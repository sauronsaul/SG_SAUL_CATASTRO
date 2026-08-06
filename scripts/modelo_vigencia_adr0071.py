#!/usr/bin/env python3
"""
Modelo ejecutable de referencia — predicados temporales de ADR-0071.

NO es codigo productivo. NO toca base de datos. Solo biblioteca estandar.
Su proposito es FALSAR la coherencia interna de ADR-0071: predicados
temporales, validadores estructurales, gates y ciclo de vida del
snapshot. Si detecta una contradiccion, el defecto puede estar en el
modelo O en la prueba, y hay que determinar cual antes de redactar.

LIMITE EXPRESO: este modelo NO determina la legalidad de nada, NO
prevalece sobre el ADR y NO es fuente normativa. Prueba consistencia
interna; la adecuacion juridica corresponde al GAM (ADR-0071 L1).
Es PROVISIONAL y no productivo.

Termina con codigo de salida distinto de cero ante cualquier contradiccion.
"""

from __future__ import annotations

import itertools
import sys
from dataclasses import dataclass, field
from datetime import date, datetime, timedelta, timezone
from typing import Optional

# ---------------------------------------------------------------------------
# Constantes del modelo
# ---------------------------------------------------------------------------

TZ_BOLIVIA = timezone(timedelta(hours=-4))  # sin horario de verano

ESTADOS = ("PropuestaTecnica", "Aprobada", "Reemplazada", "Cesada", "Descartada")
ESTADOS_CON_EFECTO_HISTORICO = ("Aprobada", "Reemplazada", "Cesada")

ROL_APRUEBA = "APRUEBA_PARAMETRO"
ROL_REGLAMENTA = "REGLAMENTA_REGLA"
ROL_HABILITA = "HABILITA_EMISION"
ROL_REFERENCIA = "REFERENCIA"
ROLES = (ROL_APRUEBA, ROL_REGLAMENTA, ROL_HABILITA, ROL_REFERENCIA)

GATE_APROBACION = "APROBACION"
GATE_PREVIEW = "PREVIEW"
GATE_EMISION = "EMISION"
GATES = (GATE_APROBACION, GATE_PREVIEW, GATE_EMISION)
GATES_CON_SNAPSHOT = (GATE_PREVIEW, GATE_EMISION)

INFINITO = None  # fin de intervalo abierto por la derecha


# ---------------------------------------------------------------------------
# Entidades
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class Instrumento:
    id: str
    municipio: str
    entrada_vigencia: date
    cese: Optional[date] = None
    acto_finalizado: bool = True  # False = borrador; no sirve para aprobar

    def ventana(self) -> tuple[date, Optional[date]]:
        return (self.entrada_vigencia, self.cese)


@dataclass(frozen=True)
class Grupo:
    """Grupo de requisito. Las alternativas dentro del grupo son OR.
    Los grupos entre si son AND, dentro del mismo rol."""
    id: str
    rol: str
    objetivo: Optional[str]          # parametro_codigo | formula_version | None
    alternativas: tuple[str, ...]    # ids de instrumento
    gestiones_objetivo: tuple[int, ...] = ()   # solo HABILITA_EMISION


@dataclass(frozen=True)
class Fundamento:
    """Fundamento de aplicacion anterior. Solo levanta el extremo INFERIOR
    y solo para los grupos que nombra expresamente."""
    id: str
    gestiones: tuple[int, ...]
    grupos_autorizados: tuple[str, ...]
    fecha_aplicacion_desde: date


@dataclass
class ParametrosVersion:
    id: str
    municipio: str
    gestion_desde: int
    gestion_hasta: Optional[int]
    estado: str
    fecha_aprobacion: Optional[date] = None
    fecha_efecto_cese: Optional[date] = None
    fecha_inicio_reemplazo: Optional[date] = None
    sucesora_id: Optional[str] = None
    grupos: tuple[Grupo, ...] = ()
    formulas_compatibles: tuple[str, ...] = ()
    fundamentos: tuple[Fundamento, ...] = ()


@dataclass(frozen=True)
class Corrida:
    id: str
    municipio: str
    parametros_version_id: str
    formula_version: str
    gestion: int
    fecha_corte: date
    iniciada_at: datetime
    resolucion_congelada: tuple[tuple[str, str], ...] = ()


# ---------------------------------------------------------------------------
# Utilidades de intervalos semiabiertos [inicio, fin)
# ---------------------------------------------------------------------------

def _fin_menor(a: Optional[date], b: Optional[date]) -> Optional[date]:
    if a is INFINITO:
        return b
    if b is INFINITO:
        return a
    return min(a, b)


def _fin_mayor(a: Optional[date], b: Optional[date]) -> Optional[date]:
    if a is INFINITO or b is INFINITO:
        return INFINITO
    return max(a, b)


def contiene(intervalo: Optional[tuple[date, Optional[date]]], f: date) -> bool:
    if intervalo is None:
        return False
    inicio, fin = intervalo
    if f < inicio:
        return False
    if fin is not INFINITO and f >= fin:
        return False
    return True


class ConfiguracionInvalida(Exception):
    """Version o grafo normativo mal formado. NO es lo mismo que False."""
    pass


class CorridaInvalida(Exception):
    """Corrida incompatible con su version, o snapshot invalido."""
    pass


def union_continua(ventanas: list[tuple[date, Optional[date]]]
                   ) -> tuple[date, Optional[date]]:
    """Union de las ventanas de las alternativas de UN grupo.

    ADR-0071 v5, restriccion de dominio: la union debe ser un unico
    intervalo continuo. Un hueco separa versiones, no se representa como
    una sola version que revive.
    """
    if not ventanas:
        raise ConfiguracionInvalida("grupo requerido sin alternativas")
    ordenadas = sorted(ventanas, key=lambda v: v[0])
    inicio, fin = ordenadas[0]
    for sig_ini, sig_fin in ordenadas[1:]:
        if fin is INFINITO:
            continue
        if sig_ini > fin:
            raise ConfiguracionInvalida(
                f"hueco entre alternativas: cierra {fin}, siguiente abre {sig_ini}")
        fin = _fin_mayor(fin, sig_fin)
    return (inicio, fin)


def interseccion(ventanas: list[tuple[date, Optional[date]]]
                 ) -> tuple[date, Optional[date]]:
    """Interseccion de las ventanas de los grupos obligatorios (AND)."""
    if not ventanas:
        raise ConfiguracionInvalida("version sin grupos formales")
    inicio = max(v[0] for v in ventanas)
    fin: Optional[date] = INFINITO
    for _, f in ventanas:
        fin = _fin_menor(fin, f)
    if fin is not INFINITO and inicio >= fin:
        raise ConfiguracionInvalida(
            f"ventana vacia: inicio {inicio} >= fin {fin}")
    return (inicio, fin)


# ---------------------------------------------------------------------------
# Resolucion de grupos
# ---------------------------------------------------------------------------

def grupos_formales(v: ParametrosVersion) -> list[Grupo]:
    """Los que sostienen formalmente la version: APRUEBA_PARAMETRO.
    REGLAMENTA_REGLA y HABILITA_EMISION son gates operativos (D4)."""
    return [g for g in v.grupos if g.rol == ROL_APRUEBA]


def ventana_formal(v: ParametrosVersion,
                   cat: dict[str, Instrumento]) -> tuple[date, Optional[date]]:
    gs = grupos_formales(v)
    if not gs:
        raise ConfiguracionInvalida("version sin grupos APRUEBA_PARAMETRO")
    por_grupo = []
    for g in gs:
        if not g.alternativas:
            raise ConfiguracionInvalida(f"grupo {g.id} sin alternativas")
        por_grupo.append(union_continua([cat[i].ventana() for i in g.alternativas]))
    return interseccion(por_grupo)


def grupo_satisfecho(g: Grupo, f: date, cat: dict[str, Instrumento],
                     fund: Optional[Fundamento] = None) -> bool:
    """Un grupo esta satisfecho en f si alguna alternativa lo cubre.

    Con fundamento, y solo si el grupo esta expresamente autorizado, se
    levanta el extremo INFERIOR. El extremo superior NUNCA se levanta.
    """
    autorizado = fund is not None and g.id in fund.grupos_autorizados
    for iid in g.alternativas:
        ins = cat[iid]
        ini, fin = ins.ventana()
        antes_del_fin = fin is INFINITO or f < fin
        despues_del_inicio = f >= ini
        if antes_del_fin and (despues_del_inicio or autorizado):
            return True
    return False


def resolucion_por_defecto(v: ParametrosVersion, f: date,
                           cat: dict[str, Instrumento],
                           fund: Optional[Fundamento] = None
                           ) -> Optional[dict[str, str]]:
    """Seleccion automatica POR DEFECTO, no autoritativa.

    El GAM puede congelar cualquier otra alternativa aplicable del mismo
    grupo; el snapshot registra la elegida, no la sugerida. Si dos
    instrumentos no son intercambiables para el requisito, no deben
    modelarse como alternativas del mismo grupo."""
    elegidos: dict[str, str] = {}
    for g in grupos_formales(v):
        autorizado = fund is not None and g.id in fund.grupos_autorizados
        elegido = None
        for iid in g.alternativas:
            ins = cat[iid]
            ini, fin = ins.ventana()
            antes_del_fin = fin is INFINITO or f < fin
            if antes_del_fin and (f >= ini or autorizado):
                elegido = iid
                break
        if elegido is None:
            return None
        elegidos[g.id] = elegido
    return elegidos


# ---------------------------------------------------------------------------
# Predicados
# ---------------------------------------------------------------------------

def cubre_gestion(v: ParametrosVersion, gestion: int) -> bool:
    if gestion < v.gestion_desde:
        return False
    if v.gestion_hasta is not None and gestion > v.gestion_hasta:
        return False
    return True


def _limites_propios_ok(v: ParametrosVersion, f: date) -> bool:
    """Cese propio y relevo. Nunca se levantan por fundamento."""
    if v.fecha_efecto_cese is not None and f >= v.fecha_efecto_cese:
        return False
    if v.fecha_inicio_reemplazo is not None and f >= v.fecha_inicio_reemplazo:
        return False
    return True


def _base_comun(v: ParametrosVersion, gestion: int, f: date) -> bool:
    """Condiciones que valen para AMBAS ramas. NO incluye la comparacion de
    la aprobacion: cada rama la compara contra una fecha distinta."""
    if not cubre_gestion(v, gestion):
        return False
    if v.estado not in ESTADOS_CON_EFECTO_HISTORICO:
        return False
    if v.fecha_aprobacion is None:
        return False
    return _limites_propios_ok(v, f)


def _base_ok(v: ParametrosVersion, gestion: int, f: date) -> bool:
    """Rama formal: la aprobacion debe ser anterior a la fecha de corte."""
    return _base_comun(v, gestion, f) and v.fecha_aprobacion < f


def vigente_para(v: ParametrosVersion, gestion: int, f: date,
                 cat: dict[str, Instrumento]) -> bool:
    """Vigencia formal historica. No recibe gate ni corrida."""
    _exigir_configuracion(v, cat)             # invalida -> excepcion, no False
    if not _base_ok(v, gestion, f):
        return False
    return contiene(ventana_formal(v, cat), f)


def fecha_local(dt: datetime) -> date:
    return dt.astimezone(TZ_BOLIVIA).date()


def aplicable_a(v: ParametrosVersion, gestion: int, c: Corrida,
                cat: dict[str, Instrumento]) -> bool:
    """Vigencia formal, o aplicacion anterior fundada.

    La rama excepcional levanta UNICAMENTE el extremo inferior de las
    ventanas instrumentales, y solo para los grupos que el fundamento
    nombra. Nunca levanta el fin instrumental, el cese propio ni el relevo.
    """
    _exigir_configuracion(v, cat)
    _exigir_corrida(v, c, cat)
    if vigente_para(v, gestion, c.fecha_corte, cat):
        return True

    f = c.fecha_corte

    # DEFECTO CORREGIDO: la rama excepcional NO puede exigir
    # fecha_aprobacion < fecha_corte. Una norma aprobada despues de la
    # fecha de corte es exactamente el caso que esta rama existe para
    # cubrir. Solo se exige el nucleo comun.
    if not _base_comun(v, gestion, f):
        return False

    _, fin = ventana_formal(v, cat)
    if fin is not INFINITO and f >= fin:
        return False                       # el extremo superior no se levanta

    if not (v.fecha_aprobacion < fecha_local(c.iniciada_at)):
        return False                       # comparacion estricta

    for fund in v.fundamentos:
        if gestion not in fund.gestiones:
            continue
        if f < fund.fecha_aplicacion_desde:
            continue                       # el fundamento acota desde cuando
        if all(grupo_satisfecho(g, f, cat, fund) for g in grupos_formales(v)):
            return True
    return False


def puede_aprobar(v: ParametrosVersion, cat: dict[str, Instrumento],
                  modelo: Optional[dict] = None) -> bool:
    """Gate de TRANSICION a Aprobada. No recibe corrida: la aprobacion
    precede conceptualmente a cualquier corrida. Si exige modelo global
    valido: no se aprueba dentro de un registro roto."""
    _exigir_configuracion(v, cat)
    _exigir_modelo(v, cat, modelo)
    if v.estado != "PropuestaTecnica":
        return False
    gs = grupos_formales(v)
    if not gs:
        return False
    return all(any(cat[i].acto_finalizado for i in g.alternativas) for g in gs)


def habilitada_para_gate(v: ParametrosVersion, c: Corrida, gate: str,
                         cat: dict[str, Instrumento],
                         modelo: Optional[dict] = None,
                         corrida_preview: Optional[Corrida] = None) -> bool:
    """Predicado operativo. Recibe la corrida; la formula entra por aqui."""
    if gate not in GATES:
        raise ValueError(f"gate desconocido: {gate}")

    if gate == GATE_APROBACION:
        # La aprobacion PRECEDE a la corrida: no se valida la corrida aqui.
        return puede_aprobar(v, cat, modelo)

    if gate == GATE_PREVIEW:
        _exigir_configuracion(v, cat)
        _exigir_modelo(v, cat, modelo)
        _exigir_corrida(v, c, cat)
        # Lo que NO se puede -> False. Lo que esta MAL REGISTRADO -> excepcion.
        if not aplicable_a(v, c.gestion, c, cat):
            return False
        for g in v.grupos:
            if g.rol == ROL_REGLAMENTA and g.objetivo == c.formula_version:
                if not grupo_satisfecho(g, c.fecha_corte, cat):
                    return False
        errs = validar_snapshot_completo(v, c, cat, GATE_PREVIEW)
        if errs:
            raise CorridaInvalida("; ".join(errs))
        return True

    if gate == GATE_EMISION:
        _exigir_configuracion(v, cat)
        _exigir_modelo(v, cat, modelo)
        _exigir_corrida(v, c, cat)
        # Emitida solo se alcanza DESPUES de PreviewListo: el llamador
        # debe aportar el snapshot con el que se alcanzo, para verificar
        # que el de emision lo EXTIENDE y no lo sustituye. Sin el, la
        # monotonia seria una validacion desconectada.
        if corrida_preview is None:
            raise CorridaInvalida(
                "no se aporto el snapshot con el que se alcanzo PreviewListo")
        errs_mono = validar_monotonia_snapshot(corrida_preview, c)
        if errs_mono:
            raise CorridaInvalida("; ".join(errs_mono))
        if not habilitada_para_gate(v, corrida_preview, GATE_PREVIEW, cat,
                                    modelo):
            return False
        # Debe existir al menos un grupo HABILITA_EMISION que cubra la
        # gestion de la corrida Y este satisfecho. Sin habilitacion para
        # esa gestion no hay emision.
        habilitantes = [g for g in v.grupos
                        if g.rol == ROL_HABILITA
                        and c.gestion in g.gestiones_objetivo]
        if not habilitantes:
            return False        # ausencia = habilitacion NO acreditada
        # Los grupos del gate son conjuntamente obligatorios (AND). El OR
        # entre alternativas ya esta dentro de grupo_satisfecho.
        if not all(grupo_satisfecho(g, c.fecha_corte, cat)
                   for g in habilitantes):
            return False
        errs = validar_snapshot_completo(v, c, cat, GATE_EMISION)
        if errs:
            raise CorridaInvalida("; ".join(errs))
        return True

    raise ValueError(f"gate desconocido: {gate}")


# ---------------------------------------------------------------------------
# Validaciones estructurales
# ---------------------------------------------------------------------------

def inicio_normativo(v: ParametrosVersion, cat: dict[str, Instrumento]) -> date:
    return ventana_formal(v, cat)[0]


def fecha_inicio_reemplazo_esperada(suc: ParametrosVersion,
                                    cat: dict[str, Instrumento]) -> date:
    """ADR-0071 D2: primer dia en que la sucesora puede satisfacer
    VigentePara por si misma."""
    return max(inicio_normativo(suc, cat),
               suc.fecha_aprobacion + timedelta(days=1))


def validar_continuidad_grupos(v: ParametrosVersion,
                               cat: dict[str, Instrumento]) -> list[str]:
    """La union de alternativas debe ser continua en todo grupo SALVO los
    de rol REFERENCIA, que no participan de ningun gate. Un grupo
    operativo con hueco es invalido, no solo los formales."""
    errs: list[str] = []
    for g in v.grupos:
        if g.rol == ROL_REFERENCIA:
            continue
        if not g.alternativas:
            errs.append(f"grupo {g.id} sin alternativas")
            continue
        try:
            union_continua([cat[i].ventana() for i in g.alternativas])
        except ConfiguracionInvalida as e:
            errs.append(f"grupo {g.id}: {e}")
    return errs


def grupos_requeridos_por_corrida(v: ParametrosVersion, c: Corrida,
                                  gate: str = GATE_EMISION) -> list[Grupo]:
    """Grupos que la corrida debe haber congelado AL LLEGAR a ese gate.

    En PreviewListo aun no interviene la habilitacion de emision, de modo
    que sus grupos no forman parte del snapshot exigido alli. Los
    REFERENCIA nunca."""
    req = list(grupos_formales(v))
    for g in v.grupos:
        if g.rol == ROL_REGLAMENTA and g.objetivo == c.formula_version:
            req.append(g)
        elif gate == GATE_EMISION and g.rol == ROL_HABILITA \
                and c.gestion in g.gestiones_objetivo:
            req.append(g)
    return req


def _autorizado_por_fundamento(v: ParametrosVersion, gid: str, gestion: int,
                               f: date) -> bool:
    for fund in v.fundamentos:
        if gestion in fund.gestiones and f >= fund.fecha_aplicacion_desde \
                and gid in fund.grupos_autorizados:
            return True
    return False


def validar_configuracion(v: ParametrosVersion,
                          cat: dict[str, Instrumento]) -> list[str]:
    """Invariantes LOCALES de una version. No mira otras versiones ni la
    corrida. NUNCA llama a un predicado: solo primitivas."""
    errs: list[str] = []

    # 0. vocabulario cerrado: un estado o un rol desconocido quedaria
    # fuera de todos los gates y pasaria inadvertido.
    if v.estado not in ESTADOS:
        errs.append(f"estado desconocido: {v.estado}")
    for g in v.grupos:
        if g.rol not in ROLES:
            errs.append(f"grupo {g.id}: rol desconocido: {g.rol}")
        if g.gestiones_objetivo and g.rol != ROL_HABILITA:
            errs.append(f"grupo {g.id}: gestiones_objetivo solo aplica a "
                        f"{ROL_HABILITA}")

    # 1. grupo.id unico dentro de la version (ADR-0071 v5).
    vistos: set[str] = set()
    for g in v.grupos:
        if g.id in vistos:
            errs.append(f"grupo.id duplicado dentro de la version: {g.id}")
        vistos.add(g.id)

    # 2. referencias de instrumento existentes y del mismo municipio
    for g in v.grupos:
        for iid in g.alternativas:
            if iid not in cat:
                errs.append(f"grupo {g.id}: instrumento {iid} no existe")
            elif cat[iid].municipio != v.municipio:
                errs.append(f"grupo {g.id}: instrumento {iid} es de otro "
                            "municipio")

    # 3. coherencia de estado terminal
    if v.estado == "Reemplazada":
        if v.sucesora_id is None:
            errs.append("Reemplazada exige sucesora")
        if v.fecha_inicio_reemplazo is None:
            errs.append("Reemplazada exige fecha_inicio_reemplazo determinada")
        if v.fecha_efecto_cese is not None:
            errs.append("Reemplazada no admite fecha_efecto_cese")
    elif v.estado == "Cesada":
        if v.fecha_efecto_cese is None:
            errs.append("Cesada exige fecha_efecto_cese")
        if v.sucesora_id is not None:
            errs.append("Cesada no admite sucesora")
        if v.fecha_inicio_reemplazo is not None:
            errs.append("Cesada no admite fecha_inicio_reemplazo")
    else:
        if v.sucesora_id is not None:
            errs.append(f"{v.estado} no admite sucesora")
        if v.fecha_efecto_cese is not None:
            errs.append(f"{v.estado} no admite fecha_efecto_cese")
        if v.fecha_inicio_reemplazo is not None:
            errs.append(f"{v.estado} no admite fecha_inicio_reemplazo")
    if v.estado in ESTADOS_CON_EFECTO_HISTORICO and v.fecha_aprobacion is None:
        errs.append(f"{v.estado} exige fecha_aprobacion")
    if v.estado not in ESTADOS_CON_EFECTO_HISTORICO \
            and v.fecha_aprobacion is not None:
        errs.append(f"{v.estado} no admite fecha_aprobacion")

    # 4. fundamentos: todo grupo autorizado debe existir y ser formal
    formales = {g.id for g in v.grupos if g.rol == ROL_APRUEBA}
    presentes = {g.id for g in v.grupos}
    for fund in v.fundamentos:
        for gid in fund.grupos_autorizados:
            if gid not in presentes:
                errs.append(f"fundamento {fund.id} autoriza un grupo "
                            f"inexistente: {gid}")
            elif gid not in formales:
                errs.append(f"fundamento {fund.id} autoriza {gid}, de rol "
                            "improcedente para la aplicacion anterior")

    # 5. continuidad y ventana, salvo REFERENCIA.
    # Se omite ante CUALQUIER error previo, no solo ante referencias sin
    # resolver: con la configuracion ya invalida, seguir produciria
    # errores derivados sin significado, y ante un instrumento ausente,
    # un KeyError en lugar de un error estructurado.
    if not errs:
        errs += validar_continuidad_grupos(v, cat)
        if not [g for g in v.grupos if g.rol == ROL_APRUEBA]:
            errs.append("version sin grupos APRUEBA_PARAMETRO")
        else:
            try:
                ventana_formal(v, cat)
            except ConfiguracionInvalida as e:
                errs.append(str(e))
    return errs


def _exigir_configuracion(v: ParametrosVersion,
                          cat: dict[str, Instrumento]) -> None:
    errs = validar_configuracion(v, cat)
    if errs:
        raise ConfiguracionInvalida("; ".join(errs))


def _exigir_corrida(v: ParametrosVersion, c: Corrida,
                    cat: dict[str, Instrumento]) -> None:
    errs = validar_corrida(v, c, cat)
    if errs:
        raise CorridaInvalida("; ".join(errs))


def _exigir_modelo(v: ParametrosVersion, cat: dict[str, Instrumento],
                   modelo: Optional[dict]) -> None:
    """Ningun flujo operativo procede dentro de un registro globalmente
    invalido. Con modelo=None se asume el registro trivial {v.id: v},
    que NO es saltarse la validacion: una version con sucesora colgante
    falla igual."""
    reg = modelo if modelo is not None else {v.id: v}
    errs: list[str] = []
    # El registro debe ser el de ESTA version: un registro ajeno, aunque
    # sea internamente valido, no acredita nada sobre v.
    if v.id not in reg:
        errs.append(f"el registro no contiene la version evaluada {v.id}")
    elif reg[v.id] != v:
        errs.append(f"el registro contiene otra version distinta bajo el "
                    f"id {v.id}")
    errs += validar_modelo(reg, cat)
    if errs:
        raise ConfiguracionInvalida("modelo global invalido: "
                                    + "; ".join(errs))


def validar_snapshot_completo(v: ParametrosVersion, c: Corrida,
                              cat: dict[str, Instrumento],
                              gate: str = GATE_PREVIEW) -> list[str]:
    """Los estados POSTERIORES a la ejecucion exigen snapshot congelado.

    LO INVOCAN GATE_PREVIEW y GATE_EMISION, cada uno con su propio
    conjunto de grupos exigibles. NO lo invocan puede_iniciar_corrida ni
    aplicable_a: la resolucion se congela AL ejecutar, y exigirla para
    decidir si se puede ejecutar seria circular.

    ADVERTENCIA DE REDUNDANCIA. La corrida se valida por TRES rutas
    distintas al evaluar GATE_PREVIEW:
        1. el propio gate, via _exigir_corrida
        2. aplicable_a, via _exigir_corrida
        3. esta funcion, que llama a validar_corrida directamente
    Es defensa en profundidad deliberada. Consecuencia para quien escriba
    mutantes: anular _exigir_corrida NO anula toda la validacion, porque
    la ruta 3 no pasa por el. Un mutante que pretenda medir la conexion
    debe atacar las tres rutas o instrumentar las llamadas."""
    if gate not in GATES_CON_SNAPSHOT:
        return [f"gate desconocido o sin snapshot exigible: {gate}"]
    if not c.resolucion_congelada:
        return ["la corrida no congelo su resolucion"]
    errs = validar_corrida(v, c, cat)
    exigidos = {g.id for g in grupos_requeridos_por_corrida(v, c, gate)}
    faltan = exigidos - {gid for gid, _ in c.resolucion_congelada}
    if faltan:
        errs.append(f"el snapshot no cubre los grupos exigidos en {gate}: "
                    f"faltan {sorted(faltan)}")
    return errs


# Todo campo de Corrida SALVO resolucion_congelada: la corrida es la
# misma a lo largo de su ciclo de vida y solo su snapshot puede crecer.
CAMPOS_IDENTIDAD_CORRIDA = (
    "id", "municipio", "parametros_version_id", "formula_version",
    "gestion", "fecha_corte", "iniciada_at",
)


def validar_monotonia_snapshot(anterior: Corrida,
                               posterior: Corrida) -> list[str]:
    """El snapshot solo CRECE entre gates: ninguna eleccion ya congelada
    puede cambiar. Si pudiera, el registro dejaria de ser el de lo que
    efectivamente se uso, que es toda su razon de existir.

    Y la corrida debe ser LA MISMA en todos sus campos, no solo en su id:
    comparar solo el id permitiria preparar una corrida con una formula,
    gestion o fecha de corte y emitirla con otra distinta conservando el
    identificador. Lo unico que puede variar es resolucion_congelada."""
    errs: list[str] = []
    for campo in CAMPOS_IDENTIDAD_CORRIDA:
        va, vp = getattr(anterior, campo), getattr(posterior, campo)
        if va != vp:
            errs.append(f"la corrida cambio {campo} entre gates: {va} -> {vp}")

    # dict() colapsaria duplicados en silencio: el mismo defecto que FV1
    # corrigio en validar_corrida y que aqui no se habia replicado.
    for etiqueta, c in (("anterior", anterior), ("posterior", posterior)):
        vistos: set[str] = set()
        for gid, _ in c.resolucion_congelada:
            if gid in vistos:
                errs.append(f"snapshot {etiqueta}: grupo {gid} congelado mas "
                            "de una vez")
            vistos.add(gid)
    if errs:
        return errs

    prev = dict(anterior.resolucion_congelada)
    post = dict(posterior.resolucion_congelada)
    for gid, iid in prev.items():
        if gid not in post:
            errs.append(f"el grupo {gid} desaparecio del snapshot")
        elif post[gid] != iid:
            errs.append(f"el grupo {gid} cambio su eleccion congelada: "
                        f"{iid} -> {post[gid]}")
    return errs


def puede_iniciar_corrida(v: ParametrosVersion, c: Corrida,
                          cat: dict[str, Instrumento],
                          modelo: Optional[dict] = None) -> bool:
    """EnEjecucion: configuracion, modelo global y corrida compatibles.
    Sin snapshot: la resolucion se congela AL ejecutar."""
    _exigir_configuracion(v, cat)
    _exigir_modelo(v, cat, modelo)
    _exigir_corrida(v, c, cat)
    return aplicable_a(v, c.gestion, c, cat)


def puede_marcar_preview_listo(v: ParametrosVersion, c: Corrida,
                               cat: dict[str, Instrumento],
                               modelo: Optional[dict] = None) -> bool:
    """PreviewListo: estado POSTERIOR a la ejecucion. Exige snapshot."""
    return habilitada_para_gate(v, c, GATE_PREVIEW, cat, modelo)


def validar_modelo(versiones: dict[str, ParametrosVersion],
                   cat: dict[str, Instrumento]) -> list[str]:
    """Invariantes GLOBALES: una version aislada no contiene las demas."""
    errs: list[str] = []

    # Integridad del registro: la clave DEBE ser el id, y el id debe ser
    # unico. Sin esto, dos entradas distintas pueden describir la misma
    # version y las aristas apuntarian a cualquiera de ellas.
    ids_vistos: dict[str, str] = {}
    for clave, v in versiones.items():
        if clave != v.id:
            errs.append(f"clave del registro '{clave}' distinta del id "
                        f"'{v.id}'")
        if v.id in ids_vistos and ids_vistos[v.id] != clave:
            errs.append(f"id de version duplicado en el registro: {v.id}")
        ids_vistos[v.id] = clave

    # Una sucesora no puede suceder a dos antecesoras: el reemplazo seria
    # ambiguo y la sucesora tiene un unico inicio.
    antecesoras: dict[str, list[str]] = {}
    for vid, v in versiones.items():
        if v.sucesora_id is not None:
            antecesoras.setdefault(v.sucesora_id, []).append(vid)
    for suc, ants in antecesoras.items():
        if len(ants) > 1:
            errs.append(f"la version {suc} es sucesora de mas de una "
                        f"antecesora: {sorted(ants)}")

    locales: dict[str, list[str]] = {}
    for vid, v in versiones.items():
        try:
            locales[vid] = validar_configuracion(v, cat)
        except Exception as e:                     # contrato: nunca propaga
            locales[vid] = [f"validacion local fallo: {type(e).__name__}: {e}"]
        errs += [f"{vid}: {e}" for e in locales[vid]]

    for vid, v in versiones.items():
        if v.sucesora_id is None:
            continue
        if v.sucesora_id not in versiones:
            errs.append(f"{vid}: la sucesora {v.sucesora_id} no existe")
            continue
        # No se compone una arista hasta que AMBOS extremos pasan su
        # validacion local: componer sobre datos malformados produce
        # errores derivados sin significado, o excepciones.
        if locales[vid] or locales[v.sucesora_id]:
            errs.append(f"{vid}->{v.sucesora_id}: arista no evaluada, algun "
                        "extremo es localmente invalido")
            continue
        try:
            errs += [f"{vid}->{v.sucesora_id}: {e}"
                     for e in validar_sucesion(v, versiones[v.sucesora_id], cat)]
        except Exception as e:
            errs.append(f"{vid}->{v.sucesora_id}: validacion de arista fallo: "
                        f"{type(e).__name__}: {e}")

    try:
        errs += validar_cadena_sucesion(versiones)
    except Exception as e:
        errs.append(f"deteccion de ciclos fallo: {type(e).__name__}: {e}")
    return sorted(set(errs))


def validar_corrida(v: ParametrosVersion, c: Corrida,
                    cat: dict[str, Instrumento]) -> list[str]:
    """Compatibilidad municipio/version/corrida y resolucion congelada."""
    errs: list[str] = []
    if c.municipio != v.municipio:
        errs.append("municipio de la corrida distinto del de la version")
    if c.parametros_version_id != v.id:
        errs.append("la corrida referencia otra version")
    if c.formula_version not in v.formulas_compatibles:
        errs.append("formula_version no compatible con la version")
    # Universo admisible: el mayor, el de EMISION. Que un grupo concreto
    # SEA exigible depende del gate y lo comprueba validar_snapshot_completo.
    requeridos = grupos_requeridos_por_corrida(v, c, GATE_EMISION)
    gs = {g.id for g in requeridos}

    if not c.resolucion_congelada:
        # Snapshot todavia no congelado: la resolucion se fija al ejecutar,
        # no antes de evaluar si la version aplica. La exigencia de que
        # exista vive en validar_snapshot_completo, que los gates
        # posteriores a la ejecucion si invocan.
        return errs

    # FV1: una eleccion por grupo. dict() colapsaria duplicados en silencio.
    vistos: set[str] = set()
    for gid, _ in c.resolucion_congelada:
        if gid in vistos:
            errs.append(f"grupo {gid} congelado mas de una vez")
        vistos.add(gid)

    congelada = dict(c.resolucion_congelada)
    if set(congelada) - gs:
        sobran = set(congelada) - gs
        if sobran:
            errs.append(f"la resolucion congela grupos no requeridos: "
                        f"{sorted(sobran)}")

    por_id = {g.id: g for g in requeridos}
    for gid, iid in congelada.items():
        if iid not in cat:
            errs.append(f"instrumento {iid} ausente del catalogo")
            continue
        ins = cat[iid]
        if ins.municipio != v.municipio:
            errs.append(f"instrumento {iid} pertenece a otro municipio")
        g = por_id.get(gid)
        if g is None:
            continue
        if iid not in g.alternativas:
            errs.append(f"instrumento {iid} no es alternativa del grupo {gid}")
            continue
        # FV2: la eleccion congelada debe ser temporalmente aplicable.
        ini, fin = ins.ventana()
        f = c.fecha_corte
        antes_del_fin = fin is INFINITO or f < fin
        autorizado = _autorizado_por_fundamento(v, gid, c.gestion, f)
        if not (antes_del_fin and (f >= ini or autorizado)):
            errs.append(f"el instrumento {iid} congelado para {gid} no es "
                        f"aplicable en {f}")
    return errs


def validar_sucesion(ant: ParametrosVersion, suc: ParametrosVersion,
                     cat: dict[str, Instrumento]) -> list[str]:
    errs: list[str] = []
    if ant.estado != "Reemplazada":
        errs.append("la antecesora no esta en Reemplazada")
    if ant.sucesora_id != suc.id:
        errs.append("sucesora_id no apunta a la sucesora")
    if ant.municipio != suc.municipio:
        errs.append("antecesora y sucesora de municipios distintos")
    if ant.id == suc.id:
        errs.append("una version no puede sucederse a si misma")
    if suc.estado not in ESTADOS_CON_EFECTO_HISTORICO:
        errs.append("la sucesora no tiene efecto historico")
    if suc.sucesora_id == ant.id:
        errs.append("ciclo de reemplazo")
    if suc.fecha_aprobacion is None:
        errs.append("la sucesora no tiene fecha de aprobacion")
    else:
        esperada = fecha_inicio_reemplazo_esperada(suc, cat)
        if ant.fecha_inicio_reemplazo != esperada:
            errs.append(f"fecha_inicio_reemplazo {ant.fecha_inicio_reemplazo} "
                        f"no coincide con la esperada {esperada}")
        # FV5: no basta calcular la fecha; la sucesora debe poder producir
        # efectos en ella.
        if suc.fecha_efecto_cese is not None and esperada >= suc.fecha_efecto_cese:
            errs.append("la sucesora ya ceso antes de la fecha de relevo")
        if suc.fecha_inicio_reemplazo is not None \
                and esperada >= suc.fecha_inicio_reemplazo:
            errs.append("la sucesora ya fue reemplazada antes de la fecha "
                        "de relevo")
        try:
            if not contiene(ventana_formal(suc, cat), esperada):
                errs.append("la ventana normativa de la sucesora no cubre la "
                            "fecha de relevo")
        except ConfiguracionInvalida as e:
            errs.append(f"sucesora con configuracion invalida: {e}")
    return errs


def _canonizar_ciclo(nodos: list[str]) -> tuple[str, ...]:
    """Rotacion canonica: un mismo ciclo recorrido desde nodos distintos
    debe producir UNA sola entrada, no una por rotacion."""
    if not nodos:
        return ()
    i = nodos.index(min(nodos))
    return tuple(nodos[i:] + nodos[:i])


def validar_cadena_sucesion(versiones: dict[str, ParametrosVersion]
                            ) -> list[str]:
    """Ciclos de CUALQUIER longitud, incluido el de un solo nodo,
    reportados UNA vez por ciclo."""
    ciclos: set[tuple[str, ...]] = set()
    for inicio in versiones:
        visto: list[str] = []
        actual: Optional[str] = inicio
        while actual is not None and actual in versiones:
            if actual in visto:
                ciclos.add(_canonizar_ciclo(visto[visto.index(actual):]))
                break
            visto.append(actual)
            actual = versiones[actual].sucesora_id
    return sorted("ciclo de reemplazo: " + " -> ".join(list(c) + [c[0]])
                  for c in ciclos)


# ---------------------------------------------------------------------------
# Infraestructura de reporte
# ---------------------------------------------------------------------------

FALLAS: list[str] = []


def afirmar(cond: bool, etiqueta: str, detalle: str = "") -> None:
    marca = "OK  " if cond else "FALLA"
    print(f"  [{marca}] {etiqueta}" + (f" — {detalle}" if detalle else ""))
    if not cond:
        FALLAS.append(etiqueta)


def seccion(titulo: str) -> None:
    print()
    print("=" * 72)
    print(titulo)
    print("=" * 72)


# ---------------------------------------------------------------------------
# Escenario base y los casos de la tabla, con precondiciones EXPLICITAS
# ---------------------------------------------------------------------------

MUN = "051201"
D = date
DT = datetime


def dt_utc(y, m, d, h=12):
    return DT(y, m, d, h, 0, tzinfo=timezone.utc)


def escenario(nombre, instrumentos, version, corrida):
    return (nombre, {i.id: i for i in instrumentos}, version, corrida)


def _v(**kw):
    base = dict(
        id="V1", municipio=MUN, gestion_desde=2024, gestion_hasta=2024,
        estado="Aprobada", fecha_aprobacion=D(2023, 12, 1),
        formulas_compatibles=("RM024-CapIV",),
    )
    base.update(kw)
    return ParametrosVersion(**base)


def _c(**kw):
    base = dict(
        id="C1", municipio=MUN, parametros_version_id="V1",
        formula_version="RM024-CapIV", gestion=2024,
        fecha_corte=D(2024, 6, 1), iniciada_at=dt_utc(2024, 6, 15),
    )
    base.update(kw)
    return Corrida(**base)


def _c_res(v, cat, gate=GATE_EMISION, **kw):
    """Corrida con su resolucion congelada por defecto para ese gate."""
    c = _c(**kw)
    congelada = []
    for g in grupos_requeridos_por_corrida(v, c, gate):
        elegido = None
        for iid in g.alternativas:
            ins = cat[iid]
            ini, fin = ins.ventana()
            if (fin is INFINITO or c.fecha_corte < fin) and (
                    c.fecha_corte >= ini
                    or _autorizado_por_fundamento(v, g.id, c.gestion,
                                                  c.fecha_corte)):
                elegido = iid
                break
        if elegido is not None:
            congelada.append((g.id, elegido))
    return Corrida(**{**c.__dict__, "resolucion_congelada": tuple(congelada)})


LEY = Instrumento("LEY", MUN, D(2024, 1, 1))
LEY_TARDIA = Instrumento("LEY_TARDIA", MUN, D(2025, 1, 1))
LEY_CESA_MAR = Instrumento("LEY_CESA_MAR", MUN, D(2024, 1, 1), cese=D(2024, 3, 1))
LEY_DESDE_MAR = Instrumento("LEY_DESDE_MAR", MUN, D(2024, 3, 1))

G_LEY = Grupo("G1", ROL_APRUEBA, "P1", ("LEY",))
G_TARDIA = Grupo("G1", ROL_APRUEBA, "P1", ("LEY_TARDIA",))
G_SEGUNDO_TARDIO = Grupo("G2", ROL_APRUEBA, "P2", ("LEY_TARDIA",))
G_CESA = Grupo("G1", ROL_APRUEBA, "P1", ("LEY_CESA_MAR",))
G_RELEVO = Grupo("G1", ROL_APRUEBA, "P1", ("LEY_CESA_MAR", "LEY_DESDE_MAR"))

FUND_G1 = Fundamento("F1", (2023, 2024), ("G1",), D(2020, 1, 1))
LEY_MAR = Instrumento("LEY_MAR", MUN, D(2024, 3, 20))
G_MAR = Grupo("G1", ROL_APRUEBA, "P1", ("LEY_MAR",))
FUND_2024 = Fundamento("F2", (2024,), ("G1",), D(2024, 1, 1))
FUND_FEB = Fundamento("F3", (2024,), ("G1",), D(2024, 2, 1))

CASOS = [
    # (n, descripcion, instrumentos, version, corrida, vigente_esp, aplicable_esp)
    (1, "Aprobada, gestion cubierta, ventana cubre fecha_corte",
     [LEY], _v(grupos=(G_LEY,)), _c(), True, True),

    (2, "Aprobada, gestion FUERA de [desde,hasta]",
     [LEY], _v(grupos=(G_LEY,)), _c(gestion=2025), False, False),

    (3, "fecha_corte anterior al inicio, SIN fundamento",
     [LEY_TARDIA], _v(gestion_desde=2023, grupos=(G_TARDIA,)),
     _c(gestion=2023, fecha_corte=D(2024, 6, 1)), False, False),

    (4, "igual que 3, CON fundamento y aprobacion anterior a iniciada_at",
     [LEY_TARDIA],
     _v(gestion_desde=2023, grupos=(G_TARDIA,), fundamentos=(FUND_G1,)),
     _c(gestion=2023, fecha_corte=D(2024, 6, 1)), False, True),

    (5, "igual que 4, aprobacion el MISMO dia que fecha_local(iniciada_at)",
     [LEY_TARDIA],
     _v(gestion_desde=2023, grupos=(G_TARDIA,), fundamentos=(FUND_G1,),
        fecha_aprobacion=D(2024, 6, 15)),
     _c(gestion=2023, fecha_corte=D(2024, 12, 1),
        iniciada_at=dt_utc(2024, 6, 15, 20)), False, False),

    (6, "igual que 4, pero un grupo requerido NO autorizado por el fundamento",
     [LEY_TARDIA],
     _v(gestion_desde=2023, grupos=(G_TARDIA, G_SEGUNDO_TARDIO),
        fundamentos=(FUND_G1,)),
     _c(gestion=2023, fecha_corte=D(2024, 6, 1)), False, False),

    (7, "Cesada con fecha_efecto_cese <= fecha_corte",
     [LEY], _v(estado="Cesada", grupos=(G_LEY,),
               fecha_efecto_cese=D(2024, 5, 1)), _c(), False, False),

    (8, "Cesada con fecha_efecto_cese > fecha_corte, instrumentos vigentes",
     [LEY], _v(estado="Cesada", grupos=(G_LEY,),
               fecha_efecto_cese=D(2024, 9, 1)), _c(), True, True),

    (9, "Reemplazada con fecha_corte < fecha_inicio_reemplazo",
     [LEY], _v(estado="Reemplazada", sucesora_id="V2", grupos=(G_LEY,),
               fecha_inicio_reemplazo=D(2024, 9, 1)), _c(), True, True),

    (10, "Reemplazada con fecha_corte >= fecha_inicio_reemplazo",
     [LEY], _v(estado="Reemplazada", sucesora_id="V2", grupos=(G_LEY,),
               fecha_inicio_reemplazo=D(2024, 3, 1)), _c(), False, False),

    (11, "un instrumento del grupo ceso, ALTERNATIVA vigente en el mismo grupo",
     [LEY_CESA_MAR, LEY_DESDE_MAR], _v(grupos=(G_RELEVO,)), _c(), True, True),

    (12, "UNICO instrumento del grupo ceso antes de fecha_corte, CON fundamento",
     [LEY_CESA_MAR], _v(grupos=(G_CESA,), fundamentos=(FUND_G1,)), _c(),
     False, False),

    (13, "PropuestaTecnica",
     [LEY], _v(estado="PropuestaTecnica", fecha_aprobacion=None,
               grupos=(G_LEY,)), _c(), False, False),

    (14, "Descartada",
     [LEY], _v(estado="Descartada", fecha_aprobacion=None, grupos=(G_LEY,)),
     _c(), False, False),

    (15, "aprobacion el MISMO dia que fecha_corte (borde inferior estricto)",
     [LEY], _v(grupos=(G_LEY,), fecha_aprobacion=D(2024, 6, 1)),
     _c(fecha_corte=D(2024, 6, 1)), False, False),

    (16, "aprobacion el dia ANTERIOR a fecha_corte (otro lado del borde)",
     [LEY], _v(grupos=(G_LEY,), fecha_aprobacion=D(2024, 5, 31)),
     _c(fecha_corte=D(2024, 6, 1)), True, True),

    (17, "aprobacion posterior a fecha_corte",
     [LEY], _v(grupos=(G_LEY,), fecha_aprobacion=D(2024, 7, 1)),
     _c(fecha_corte=D(2024, 6, 1)), False, False),

    (18, "RETROACTIVIDAD: aprobacion POSTERIOR a fecha_corte, con fundamento",
     [LEY_MAR], _v(grupos=(G_MAR,), fecha_aprobacion=D(2024, 3, 15),
                   fundamentos=(FUND_2024,)),
     _c(fecha_corte=D(2024, 1, 1), iniciada_at=dt_utc(2024, 6, 15)),
     False, True),

    (19, "igual que 18 SIN fundamento",
     [LEY_MAR], _v(grupos=(G_MAR,), fecha_aprobacion=D(2024, 3, 15)),
     _c(fecha_corte=D(2024, 1, 1), iniciada_at=dt_utc(2024, 6, 15)),
     False, False),

    (20, "igual que 18 pero fecha_corte anterior a fecha_aplicacion_desde",
     [LEY_MAR], _v(grupos=(G_MAR,), fecha_aprobacion=D(2024, 3, 15),
                   fundamentos=(FUND_FEB,)),
     _c(fecha_corte=D(2024, 1, 1), iniciada_at=dt_utc(2024, 6, 15)),
     False, False),
]


def correr_casos() -> None:
    seccion("BLOQUE 1 — LOS CASOS DE LA TABLA DE ADR-0071 D3")
    print("Precondicion comun declarada: toda condicion no mencionada en la")
    print("descripcion del caso se cumple, y no existe fundamento salvo que")
    print("el caso lo diga expresamente.")
    print()
    for n, desc, instrumentos, v, c, vig_esp, apl_esp in CASOS:
        cat = {i.id: i for i in instrumentos}
        try:
            vig = vigente_para(v, c.gestion, c.fecha_corte, cat)
            apl = aplicable_a(v, c.gestion, c, cat)
        except ConfiguracionInvalida as e:
            print(f"  [FALLA] caso {n:>2} — configuracion invalida: {e}")
            FALLAS.append(f"caso {n} configuracion invalida")
            continue
        ok = (vig == vig_esp) and (apl == apl_esp)
        marca = "OK  " if ok else "FALLA"
        print(f"  [{marca}] caso {n:>2}  vigente={str(vig):<5} "
              f"esperado={str(vig_esp):<5} | aplicable={str(apl):<5} "
              f"esperado={str(apl_esp):<5}")
        print(f"          {desc}")
        if not ok:
            FALLAS.append(f"caso {n}")


# ---------------------------------------------------------------------------
# Bloque 2 — Propiedades sobre producto finito exhaustivo
# ---------------------------------------------------------------------------

F_CORTE = D(2024, 6, 1)

VENTANAS_INSTRUMENTO = {
    "antes":   Instrumento("I", MUN, D(2024, 9, 1)),               # inicio > f
    "dentro":  Instrumento("I", MUN, D(2024, 1, 1)),               # cubre f
    "despues": Instrumento("I", MUN, D(2024, 1, 1), cese=D(2024, 3, 1)),  # fin <= f
}
CESES = {"sin": None, "vencido": D(2024, 5, 1), "futuro": D(2024, 9, 1)}
RELEVOS = {"sin": None, "vencido": D(2024, 5, 1), "futuro": D(2024, 9, 1)}
APROBACIONES = {"anterior": D(2023, 12, 1), "mismo_dia": D(2024, 6, 15)}


def producto_configuraciones():
    """Producto finito de configuraciones COHERENTES.

    Las combinaciones incoherentes de estado y fechas terminales ya no se
    generan aqui: son configuracion invalida y se prueban por separado en
    el bloque estructural.
    """
    G = Grupo("G1", ROL_APRUEBA, "P1", ("I",))
    for estado in ESTADOS:
        if estado == "Cesada":
            terminales = [("cese", k) for k in ("vencido", "futuro")]
        elif estado == "Reemplazada":
            terminales = [("relevo", k) for k in ("vencido", "futuro")]
        else:
            terminales = [("ninguno", "sin")]
        historico = estado in ESTADOS_CON_EFECTO_HISTORICO
        # Sin efecto historico la aprobacion es None: variarla generaria
        # objetos identicos con etiquetas distintas.
        aprobaciones = list(APROBACIONES) if historico else ["n/a"]
        for (clase, val), gest, vent, tiene_f, aprob in itertools.product(
                terminales, ("dentro", "fuera"), VENTANAS_INSTRUMENTO,
                (False, True), aprobaciones):
            cat = {"I": VENTANAS_INSTRUMENTO[vent]}
            v = ParametrosVersion(
                id="V", municipio=MUN, gestion_desde=2024, gestion_hasta=2024,
                estado=estado,
                fecha_aprobacion=(APROBACIONES[aprob] if historico else None),
                fecha_efecto_cese=(CESES[val] if clase == "cese" else None),
                fecha_inicio_reemplazo=(RELEVOS[val] if clase == "relevo"
                                        else None),
                sucesora_id=("V2" if clase == "relevo" else None),
                grupos=(G,),
                formulas_compatibles=("RM024-CapIV",),
                fundamentos=((Fundamento("F", (2024,), ("G1",), D(2020, 1, 1)),)
                             if tiene_f else ()),
            )
            gestion = 2024 if gest == "dentro" else 2025
            c = Corrida(id="C1", municipio=MUN, parametros_version_id="V",
                        formula_version="RM024-CapIV", gestion=gestion,
                        fecha_corte=F_CORTE, iniciada_at=dt_utc(2024, 6, 15))
            etiqueta = (f"estado={estado} {clase}={val} gestion={gest} "
                        f"ventana={vent} fund={tiene_f} aprob={aprob}")
            yield etiqueta, v, c, cat


def correr_propiedades_producto() -> None:
    seccion("BLOQUE 2 — PROPIEDADES SOBRE PRODUCTO FINITO EXHAUSTIVO")
    total = 0
    viol_a: list[str] = []
    viol_b: list[str] = []
    hubo_diferencia_por_fundamento = 0
    hubo_aprobada_sin_vigencia = 0

    for etiqueta, v, c, cat in producto_configuraciones():
        total += 1
        f = c.fecha_corte
        try:
            inicio, fin = ventana_formal(v, cat)
        except ConfiguracionInvalida:
            continue
        apl_con = aplicable_a(v, c.gestion, c, cat)
        v_sin = ParametrosVersion(**{**v.__dict__, "fundamentos": ()})
        apl_sin = aplicable_a(v_sin, c.gestion, c, cat)
        vig = vigente_para(v, c.gestion, f, cat)

        # P-A: ningun limite SUPERIOR puede levantarse por aplicacion anterior
        supero_algun_fin = (
            (fin is not INFINITO and f >= fin)
            or (v.fecha_efecto_cese is not None and f >= v.fecha_efecto_cese)
            or (v.fecha_inicio_reemplazo is not None and f >= v.fecha_inicio_reemplazo)
        )
        if supero_algun_fin and apl_con:
            viol_a.append(etiqueta)

        # P-B: el fundamento solo cambia el resultado si fallaba el INFERIOR
        if apl_con != apl_sin:
            hubo_diferencia_por_fundamento += 1
            fallaba_solo_el_inferior = (
                _base_ok(v, c.gestion, f)
                and (fin is INFINITO or f < fin)
                and f < inicio
            )
            if not fallaba_solo_el_inferior:
                viol_b.append(etiqueta)

        # P-D: aprobacion no implica vigencia (se cuenta un testigo).
        # El registro incluye la sucesora declarada, si la hay: de lo
        # contrario el modelo global seria invalido por sucesora colgante.
        reg = {v.id: v}
        if v.sucesora_id is not None:
            reg[v.sucesora_id] = ParametrosVersion(
                id=v.sucesora_id, municipio=v.municipio,
                gestion_desde=v.gestion_desde, gestion_hasta=v.gestion_hasta,
                estado="Aprobada", fecha_aprobacion=D(2024, 1, 2),
                grupos=v.grupos, formulas_compatibles=v.formulas_compatibles)
        try:
            aprob_ok = habilitada_para_gate(v, c, GATE_APROBACION, cat, reg)
        except (ConfiguracionInvalida, CorridaInvalida):
            aprob_ok = False
        if aprob_ok and not vig:
            hubo_aprobada_sin_vigencia += 1

    unicas = set()
    for _e, v, c, cat in producto_configuraciones():
        unicas.add((v.estado, v.gestion_desde, v.gestion_hasta,
                    v.fecha_aprobacion, v.fecha_efecto_cese,
                    v.fecha_inicio_reemplazo, v.sucesora_id,
                    tuple(sorted((k, i.entrada_vigencia, i.cese)
                                 for k, i in cat.items())),
                    len(v.fundamentos), c.gestion, c.fecha_corte))
    print(f"  configuraciones evaluadas: {total}")
    print(f"  configuraciones semanticamente distintas: {len(unicas)}")
    afirmar(len(unicas) == total,
            "P-U el producto no genera configuraciones duplicadas",
            f"{total} generadas, {len(unicas)} distintas")
    print(f"  configuraciones donde el fundamento cambio el resultado: "
          f"{hubo_diferencia_por_fundamento}")
    print(f"  configuraciones con aprobacion sin vigencia: "
          f"{hubo_aprobada_sin_vigencia}")
    print()
    afirmar(not viol_a,
            "P-A ningun cese, fin instrumental ni relevo se levanta por fundamento",
            f"{len(viol_a)} violaciones" if viol_a else "")
    for e in viol_a[:5]:
        print(f"          violacion: {e}")
    afirmar(not viol_b,
            "P-B el fundamento solo cambia el resultado si fallaba el extremo inferior",
            f"{len(viol_b)} violaciones" if viol_b else "")
    for e in viol_b[:5]:
        print(f"          violacion: {e}")
    afirmar(hubo_diferencia_por_fundamento > 0,
            "P-B' el fundamento cambia el resultado en al menos un caso",
            "si nunca cambiara, la rama excepcional seria codigo muerto")
    afirmar(hubo_aprobada_sin_vigencia > 0,
            "P-D aprobacion no implica vigencia",
            f"{hubo_aprobada_sin_vigencia} testigos")


# ---------------------------------------------------------------------------
# Bloque 3 — Propiedades estructurales
# ---------------------------------------------------------------------------

def correr_propiedades_estructurales() -> None:
    seccion("BLOQUE 3 — PROPIEDADES ESTRUCTURALES")

    # P-C / P-G — ventana continua
    a1 = Instrumento("A1", MUN, D(2024, 1, 1), cese=D(2024, 3, 1))
    a2 = Instrumento("A2", MUN, D(2024, 6, 1))          # hueco marzo-junio
    cat = {"A1": a1, "A2": a2}
    g = Grupo("G1", ROL_APRUEBA, "P1", ("A1", "A2"))
    v = _v(grupos=(g,))
    try:
        ventana_formal(v, cat)
        afirmar(False, "P-C/P-G una alternativa con hueco obliga a dividir la version",
                "la configuracion discontinua NO fue rechazada")
    except ConfiguracionInvalida as e:
        afirmar(True, "P-C/P-G una alternativa con hueco obliga a dividir la version",
                str(e))

    # contiguo exacto SI es valido
    b2 = Instrumento("B2", MUN, D(2024, 3, 1))
    catb = {"A1": a1, "B2": b2}
    gb = Grupo("G1", ROL_APRUEBA, "P1", ("A1", "B2"))
    try:
        w = ventana_formal(_v(grupos=(gb,)), catb)
        afirmar(w == (D(2024, 1, 1), INFINITO),
                "alternativas contiguas exactas producen una ventana unica", str(w))
    except ConfiguracionInvalida as e:
        afirmar(False, "alternativas contiguas exactas producen una ventana unica",
                str(e))

    # P-E — grupo requerido vacio
    gv = Grupo("G1", ROL_APRUEBA, "P1", ())
    try:
        ventana_formal(_v(grupos=(gv,)), {})
        afirmar(False, "P-E ningun grupo requerido vacio puede pasar")
    except ConfiguracionInvalida as e:
        afirmar(True, "P-E ningun grupo requerido vacio puede pasar", str(e))

    # version sin grupos formales
    try:
        ventana_formal(_v(grupos=()), {})
        afirmar(False, "P-E' version sin grupos formales es invalida")
    except ConfiguracionInvalida as e:
        afirmar(True, "P-E' version sin grupos formales es invalida", str(e))

    # P-F — predecesora y sucesora no se solapan.
    # La no superposicion debe SALIR del modelo, no ser supuesto de la prueba.
    cat2 = {"LEY": LEY}
    v2 = _v(id="V2", fecha_aprobacion=D(2024, 8, 1), grupos=(G_LEY,))
    relevo = fecha_inicio_reemplazo_esperada(v2, cat2)
    v1 = _v(id="V1", estado="Reemplazada", sucesora_id="V2",
            fecha_inicio_reemplazo=relevo, grupos=(G_LEY,))
    errs = validar_sucesion(v1, v2, cat2)
    afirmar(not errs, "P-F0 la sucesion bien construida valida sin errores",
            "; ".join(errs) if errs else f"relevo={relevo}")

    def _solapes(ant, suc):
        out = []
        for dias in range(0, 400):
            f = D(2024, 1, 1) + timedelta(days=dias)
            if vigente_para(ant, 2024, f, cat2) and vigente_para(suc, 2024, f, cat2):
                out.append(f)
        return out

    solapan = _solapes(v1, v2)
    afirmar(not solapan, "P-F predecesora y sucesora no se solapan",
            f"{len(solapan)} fechas solapadas" if solapan
            else "400 fechas probadas, sin supuesto anadido")

    # contraprueba: si el relevo se fija mal, DEBE aparecer solapamiento
    v1_mal = _v(id="V1", estado="Reemplazada", sucesora_id="V2",
                fecha_inicio_reemplazo=D(2024, 12, 1), grupos=(G_LEY,))
    errs_mal = validar_sucesion(v1_mal, v2, cat2)
    afirmar(bool(errs_mal) and bool(_solapes(v1_mal, v2)),
            "P-F' un relevo mal fijado produce solapamiento Y es rechazado",
            f"errores={len(errs_mal)} solapes={len(_solapes(v1_mal, v2))}")

    # --- validacion de corrida: municipio, version, formula, resolucion ---
    gA_ = Grupo("G1", ROL_APRUEBA, "P1", ("LEY",))
    vC = _v(id="V1", grupos=(gA_,))
    c_ok = _c(resolucion_congelada=(("G1", "LEY"),))
    afirmar(not validar_corrida(vC, c_ok, cat2),
            "V-1 una corrida bien formada valida sin errores")
    afirmar(bool(validar_corrida(vC, _c(municipio="022001",
                                       resolucion_congelada=(("G1", "LEY"),)),
                                 cat2)),
            "V-2 se rechaza una corrida de otro municipio")
    afirmar(bool(validar_corrida(vC, _c(parametros_version_id="VX",
                                       resolucion_congelada=(("G1", "LEY"),)),
                                 cat2)),
            "V-3 se rechaza una corrida que referencia otra version")
    afirmar(not validar_corrida(vC, _c(), cat2),
            "V-4 una corrida aun sin congelar es compatible: el snapshot se "
            "fija al ejecutar")
    afirmar(bool(validar_snapshot_completo(vC, _c(), cat2)),
            "V-4' pero el snapshot incompleto SI se rechaza al registrarlo")
    ley_ajena = Instrumento("LEY_AJENA", "022001", D(2024, 1, 1))
    gAj = Grupo("G1", ROL_APRUEBA, "P1", ("LEY_AJENA",))
    afirmar(bool(validar_corrida(_v(id="V1", grupos=(gAj,)),
                                 _c(resolucion_congelada=(("G1", "LEY_AJENA"),)),
                                 {"LEY_AJENA": ley_ajena})),
            "V-5 se rechaza un instrumento de otro municipio en la resolucion")

    # --- HABILITA_EMISION aplica su objetivo de gestion ---
    hab24 = Grupo("GH", ROL_HABILITA, "2024", ("LEY",), (2024,))
    vH24 = _v(grupos=(G_LEY, hab24))
    afirmar(habilitada_para_gate(
                vH24, _c_res(vH24, cat2, gestion=2024), GATE_EMISION, cat2,
                None, _c_res(vH24, cat2, GATE_PREVIEW, gestion=2024)),
            "H-1 emision habilitada para la gestion cubierta")
    vH24b = _v(gestion_hasta=2025, grupos=(G_LEY, hab24))
    afirmar(not habilitada_para_gate(
                vH24b, _c_res(vH24b, cat2, gestion=2025), GATE_EMISION, cat2,
                None, _c_res(vH24b, cat2, GATE_PREVIEW, gestion=2025)),
            "H-2 emision NO habilitada para una gestion no cubierta por el grupo")
    vsin = _v(grupos=(G_LEY,))
    afirmar(not habilitada_para_gate(vsin, _c_res(vsin, cat2), GATE_EMISION,
                                     cat2, None,
                                     _c_res(vsin, cat2, GATE_PREVIEW)),
            "H-3 sin ningun HABILITA_EMISION no hay emision")
    hab_ok = Grupo("GH1", ROL_HABILITA, "2024", ("LEY",), (2024,))
    hab_no = Grupo("GH2", ROL_HABILITA, "2024", ("LEY_TARDIA",), (2024,))
    catH = {"LEY": LEY, "LEY_TARDIA": LEY_TARDIA}
    vh4 = _v(grupos=(G_LEY, hab_ok, hab_no))
    afirmar(not habilitada_para_gate(
                vh4, _c_res(vh4, catH, gestion=2024), GATE_EMISION, catH,
                None, _c_res(vh4, catH, GATE_PREVIEW, gestion=2024)),
            "H-4 dos grupos habilitantes, uno insatisfecho: NO hay emision",
            "los grupos del gate son AND")
    vh4b = _v(grupos=(G_LEY, hab_ok))
    afirmar(habilitada_para_gate(
                vh4b, _c_res(vh4b, catH, gestion=2024), GATE_EMISION, catH,
                None, _c_res(vh4b, catH, GATE_PREVIEW, gestion=2024)),
            "H-4' un solo grupo habilitante satisfecho: SI hay emision")

    # P-H — un instrumento puede satisfacer varios grupos, uno por grupo
    gA = Grupo("GA", ROL_APRUEBA, "P1", ("LEY",))
    gB = Grupo("GB", ROL_APRUEBA, "P2", ("LEY",))
    vH = _v(grupos=(gA, gB))
    r = resolucion_por_defecto(vH, F_CORTE, cat2)
    afirmar(r == {"GA": "LEY", "GB": "LEY"},
            "P-H un instrumento satisface varios grupos sin duplicarse en uno",
            str(r))

    # P-J — sobre una CORRIDA real, no sobre la sugerencia por defecto
    cH = _c_res(vH, cat2, GATE_EMISION)
    congelada_pj = dict(cH.resolucion_congelada)
    afirmar(len(congelada_pj) == len(grupos_requeridos_por_corrida(
                vH, cH, GATE_EMISION))
            and not validar_snapshot_completo(vH, cH, cat2, GATE_EMISION),
            "P-J la CORRIDA congela un instrumento por grupo requerido",
            f"snapshot={sorted(congelada_pj.items())}")

    # P-I — la formula elegida determina sus grupos de reglamentacion
    reg_ok = Grupo("GR1", ROL_REGLAMENTA, "RM024-CapIV", ("LEY",))
    reg_falla = Grupo("GR2", ROL_REGLAMENTA, "RM024-CapVI", ("LEY_TARDIA",))
    catI = {"LEY": LEY, "LEY_TARDIA": LEY_TARDIA}
    vI = _v(grupos=(G_LEY, reg_ok, reg_falla),
            formulas_compatibles=("RM024-CapIV", "RM024-CapVI"))
    prev_iv = habilitada_para_gate(
        vI, _c_res(vI, catI, GATE_PREVIEW, formula_version="RM024-CapIV"),
        GATE_PREVIEW, catI)
    prev_vi = habilitada_para_gate(
        vI, _c_res(vI, catI, GATE_PREVIEW, formula_version="RM024-CapVI"),
        GATE_PREVIEW, catI)
    afirmar(prev_iv and not prev_vi,
            "P-I la formula elegida determina que grupos REGLAMENTA_REGLA se exigen",
            f"CapIV={prev_iv} CapVI={prev_vi}")

    # formula no compatible: NO es False, es corrida invalida
    vNC = _v(grupos=(G_LEY,), formulas_compatibles=("RM024-CapIV",))
    try:
        habilitada_para_gate(vNC, _c(formula_version="RM024-CapVI"),
                             GATE_PREVIEW, cat2)
        afirmar(False, "una formula no compatible produce CorridaInvalida")
    except CorridaInvalida as e:
        afirmar(True, "una formula no compatible produce CorridaInvalida",
                str(e))

    # --- CONEXION: los gates NO pueden aprobar lo que el validador rechaza ---
    c_ajena = _c(municipio="022001")
    errs_conn = validar_corrida(_v(id="V1", grupos=(G_LEY,)), c_ajena, cat2)
    afirmar(bool(errs_conn),
            "C-1 validar_corrida rechaza una corrida de otro municipio",
            "; ".join(errs_conn))
    try:
        habilitada_para_gate(_v(id="V1", grupos=(G_LEY,)), c_ajena,
                             GATE_PREVIEW, cat2)
        afirmar(False, "C-2 el gate NO devuelve True sobre esa corrida",
                "el gate la acepto: validador desconectado")
    except CorridaInvalida:
        afirmar(True, "C-2 el gate rechaza la corrida que el validador rechaza")

    # configuracion invalida -> excepcion, nunca False
    gdup1 = Grupo("GX", ROL_APRUEBA, "P1", ("LEY",))
    gdup2 = Grupo("GX", ROL_REGLAMENTA, "RM024-CapIV", ("LEY",))
    vdup = _v(grupos=(gdup1, gdup2))
    afirmar(bool(validar_configuracion(vdup, cat2)),
            "C-3 grupo.id duplicado dentro de la version se rechaza")
    try:
        vigente_para(vdup, 2024, F_CORTE, cat2)
        afirmar(False, "C-4 VigentePara no responde sobre configuracion invalida",
                "devolvio un booleano")
    except ConfiguracionInvalida:
        afirmar(True, "C-4 VigentePara lanza ConfiguracionInvalida, no False")

    fund_colgante = Fundamento("FX", (2024,), ("NO_EXISTE",), D(2020, 1, 1))
    vfc = _v(grupos=(G_LEY,), fundamentos=(fund_colgante,))
    afirmar(bool(validar_configuracion(vfc, cat2)),
            "C-5 un fundamento que autoriza un grupo inexistente se rechaza")
    fund_rol = Fundamento("FY", (2024,), ("GR",), D(2020, 1, 1))
    vfr = _v(grupos=(G_LEY, Grupo("GR", ROL_REGLAMENTA, "RM024-CapIV",
                                  ("LEY",))), fundamentos=(fund_rol,))
    afirmar(bool(validar_configuracion(vfr, cat2)),
            "C-6 un fundamento que autoriza un grupo de rol improcedente se rechaza")

    ins_ajeno = Instrumento("AJENO", "022001", D(2024, 1, 1))
    vaj = _v(grupos=(Grupo("G1", ROL_APRUEBA, "P1", ("AJENO",)),))
    afirmar(bool(validar_configuracion(vaj, {"AJENO": ins_ajeno})),
            "C-7 un instrumento formal de otro municipio invalida la version")
    try:
        vigente_para(vaj, 2024, F_CORTE, {"AJENO": ins_ajeno})
        afirmar(False, "C-8 el predicado no avanza sobre municipio cruzado")
    except ConfiguracionInvalida:
        afirmar(True, "C-8 el predicado lanza excepcion sobre municipio cruzado")

    # APROBACION no depende de la corrida
    vpa = _v(estado="PropuestaTecnica", fecha_aprobacion=None, grupos=(G_LEY,))
    afirmar(puede_aprobar(vpa, cat2),
            "C-9 puede_aprobar no recibe corrida y funciona sin ella")
    afirmar(habilitada_para_gate(vpa, _c(municipio="022001"),
                                 GATE_APROBACION, cat2),
            "C-10 GATE_APROBACION ignora la corrida, incluso invalida")

    # validacion GLOBAL vs LOCAL
    vg1 = _v(id="VA", estado="Reemplazada", sucesora_id="VB",
             fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))
    afirmar(bool(validar_modelo({"VA": vg1}, cat2)),
            "C-11 validar_modelo detecta una sucesora inexistente")
    afirmar(not validar_configuracion(vg1, cat2),
            "C-11' la misma version es LOCALMENTE valida: son dos niveles")
    vb_mala = _v(id="VB", estado="Cesada", fecha_efecto_cese=D(2024, 8, 1),
                 grupos=(G_LEY,))
    afirmar(bool(validar_modelo({"VA": vg1, "VB": vb_mala}, cat2)),
            "C-12 validar_modelo compone las aristas, no solo busca ciclos")

    # C-13: contrato de validar_modelo — devuelve errores, nunca excepcion
    vfalta = _v(id="VZ", grupos=(Grupo("GM", ROL_APRUEBA, "P1", ("FALTA",)),))
    try:
        errs13 = validar_modelo({"VZ": vfalta}, {})
        afirmar(bool(errs13),
                "C-13 validar_modelo devuelve errores estructurados, no KeyError",
                "; ".join(errs13))
    except Exception as e:
        afirmar(False, "C-13 validar_modelo devuelve errores estructurados, "
                "no KeyError", f"{type(e).__name__}: {e}")

    # C-14: no se compone una arista si un extremo es localmente invalido
    vA14 = _v(id="VA", estado="Reemplazada", sucesora_id="VB",
              fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))
    vB14 = _v(id="VB", grupos=(Grupo("GM", ROL_APRUEBA, "P1", ("FALTA",)),))
    errs14 = validar_modelo({"VA": vA14, "VB": vB14}, cat2)
    afirmar(any("arista no evaluada" in e for e in errs14),
            "C-14 la arista no se compone sobre un extremo invalido",
            "; ".join(errs14))

    # C-15: ciclo de vida del snapshot, tres estados
    v15 = _v(id="V1", grupos=(G_LEY,))
    afirmar(puede_iniciar_corrida(v15, _c(), cat2),
            "C-15 EnEjecucion: se puede iniciar SIN snapshot congelado")
    try:
        puede_marcar_preview_listo(v15, _c(), cat2)
        afirmar(False, "C-16 PreviewListo exige snapshot congelado")
    except CorridaInvalida as e:
        afirmar(True, "C-16 PreviewListo exige snapshot congelado", str(e))
    afirmar(puede_marcar_preview_listo(v15, _c_res(v15, cat2, GATE_PREVIEW),
                                       cat2),
            "C-16' con snapshot congelado, PreviewListo procede")

    # --- PB: testigos DIRIGIDOS de la rama excepcional, por clase ---
    # Un producto pequeno da pocos testigos. Estos cubren las clases de
    # equivalencia, que es lo que importa en una propiedad existencial.
    cat_pb = {"LEY_MAR": LEY_MAR}

    def _pb(fund, **kw):
        v = _v(grupos=(G_MAR,), fecha_aprobacion=D(2024, 3, 15),
               fundamentos=((fund,) if fund else ()), **kw)
        c = _c(fecha_corte=D(2024, 1, 1), iniciada_at=dt_utc(2024, 6, 15))
        return aplicable_a(v, 2024, c, cat_pb)

    f_antes = Fundamento("FA", (2024,), ("G1",), D(2023, 1, 1))
    f_igual = Fundamento("FI", (2024,), ("G1",), D(2024, 1, 1))
    f_desp = Fundamento("FD", (2024,), ("G1",), D(2024, 2, 1))
    afirmar(_pb(f_antes) and _pb(f_igual) and not _pb(f_desp),
            "PB-1 fecha_aplicacion_desde: antes SI, igual SI, despues NO",
            "el borde inferior del fundamento es inclusivo")

    g2 = Grupo("G2", ROL_APRUEBA, "P2", ("LEY_MAR",))
    v_par = _v(grupos=(G_MAR, g2), fecha_aprobacion=D(2024, 3, 15),
               fundamentos=(Fundamento("FP", (2024,), ("G1",), D(2023, 1, 1)),))
    v_tot = _v(grupos=(G_MAR, g2), fecha_aprobacion=D(2024, 3, 15),
               fundamentos=(Fundamento("FT", (2024,), ("G1", "G2"),
                                       D(2023, 1, 1)),))
    c_pb = _c(fecha_corte=D(2024, 1, 1), iniciada_at=dt_utc(2024, 6, 15))
    afirmar(not aplicable_a(v_par, 2024, c_pb, cat_pb)
            and aplicable_a(v_tot, 2024, c_pb, cat_pb),
            "PB-2 autorizacion parcial de grupos NO alcanza; total SI")

    # aprobacion antes / mismo dia / despues de fecha_corte, siempre
    # anterior a iniciada_at
    def _pb_aprob(fa, fcorte):
        v = _v(grupos=(G_MAR,), fecha_aprobacion=fa,
               fundamentos=(f_antes,))
        return aplicable_a(v, 2024, _c(fecha_corte=fcorte,
                                       iniciada_at=dt_utc(2024, 6, 15)),
                           cat_pb)
    afirmar(_pb_aprob(D(2024, 1, 5), D(2024, 1, 10))
            and _pb_aprob(D(2024, 1, 10), D(2024, 1, 10))
            and _pb_aprob(D(2024, 3, 15), D(2024, 1, 10)),
            "PB-3 la COMPARACION DE APROBACION usa iniciada_at, no "
            "fecha_corte",
            "fecha_corte sigue gobernando gestion, limites, ventana y "
            "fundamento; lo que no gobierna es esta comparacion")

    # ventana superior: abierta / cese instrumental / relevo propio.
    # Para que el cese instrumental sea el limite en juego, la fecha de
    # corte debe ser POSTERIOR al cese, no anterior al inicio.
    ley_cesa = Instrumento("LC", MUN, D(2024, 1, 5), cese=D(2024, 4, 1))
    v_ces = _v(grupos=(Grupo("G1", ROL_APRUEBA, "P1", ("LC",)),),
               fecha_aprobacion=D(2024, 3, 15), fundamentos=(f_antes,))
    c_post = _c(fecha_corte=D(2024, 5, 1), iniciada_at=dt_utc(2024, 6, 15))
    v_rel = _v(grupos=(G_MAR,), fecha_aprobacion=D(2024, 3, 15),
               estado="Reemplazada", sucesora_id="V2",
               fecha_inicio_reemplazo=D(2023, 6, 1), fundamentos=(f_antes,))
    afirmar(_pb(f_antes),
            "PB-4a ventana superior abierta: la excepcion procede")
    afirmar(not aplicable_a(v_ces, 2024, c_post, {"LC": ley_cesa}),
            "PB-4b cese instrumental anterior a fecha_corte: NO procede")
    afirmar(not aplicable_a(v_rel, 2024, c_pb, cat_pb),
            "PB-4c relevo propio anterior a fecha_corte: NO procede")

    # --- BLOQUEANTES DE LA CUARTA PASADA ---
    v_mono = _v(id="V1", grupos=(G_LEY, Grupo("GR", ROL_REGLAMENTA,
                                              "RM024-CapIV", ("LEY",)),
                                 Grupo("GH", ROL_HABILITA, "2024",
                                       ("LEY",), (2024,))))
    c_prev = _c_res(v_mono, cat2, GATE_PREVIEW)
    c_emi_ok = _c_res(v_mono, cat2, GATE_EMISION)
    afirmar(not validar_monotonia_snapshot(c_prev, c_emi_ok),
            "B1-a el snapshot puede CRECER de Preview a Emision",
            f"{len(c_prev.resolucion_congelada)} -> "
            f"{len(c_emi_ok.resolucion_congelada)}")
    ley_alt = Instrumento("LEY_ALT", MUN, D(2024, 1, 1))
    c_emi_mal = Corrida(**{**c_emi_ok.__dict__,
                           "resolucion_congelada": tuple(
                               (g, "LEY_ALT" if g == "G1" else i)
                               for g, i in c_emi_ok.resolucion_congelada)})
    afirmar(bool(validar_monotonia_snapshot(c_prev, c_emi_mal)),
            "B1-b una eleccion ya congelada NO puede cambiar",
            "; ".join(validar_monotonia_snapshot(c_prev, c_emi_mal)))
    c_emi_menos = Corrida(**{**c_emi_ok.__dict__,
                             "resolucion_congelada": c_prev.resolucion_congelada[:1]})
    afirmar(bool(validar_monotonia_snapshot(c_prev, c_emi_menos)),
            "B1-c un grupo ya congelado NO puede desaparecer")

    v_colg = _v(id="VA", estado="Reemplazada", sucesora_id="NO_EXISTE",
                fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))
    afirmar(not validar_configuracion(v_colg, cat2),
            "B2-a la version es LOCALMENTE valida")
    try:
        habilitada_para_gate(v_colg, _c_res(v_colg, cat2, GATE_PREVIEW),
                             GATE_PREVIEW, cat2)
        afirmar(False, "B2-b el gate NO opera dentro de un modelo global invalido")
    except ConfiguracionInvalida as e:
        afirmar(True, "B2-b el gate NO opera dentro de un modelo global invalido",
                str(e))
    try:
        puede_iniciar_corrida(v_colg, _c(), cat2)
        afirmar(False, "B2-c tampoco se puede iniciar una corrida")
    except ConfiguracionInvalida:
        afirmar(True, "B2-c tampoco se puede iniciar una corrida")

    v_ok3 = _v(id="V1", grupos=(G_LEY,))
    afirmar(bool(validar_modelo({"OTRA_CLAVE": v_ok3}, cat2)),
            "B3-a la clave del registro debe ser el id de la version")
    afirmar(bool(validar_modelo({"V1": v_ok3, "V1b": _v(id="V1",
                                                        grupos=(G_LEY,))},
                                cat2)),
            "B3-b un id de version duplicado en el registro se rechaza")
    va_c = _v(id="VA", estado="Reemplazada", sucesora_id="VC",
              fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))
    vb_c = _v(id="VB", estado="Reemplazada", sucesora_id="VC",
              fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))
    vc_c = _v(id="VC", grupos=(G_LEY,))
    errs_conv = validar_modelo({"VA": va_c, "VB": vb_c, "VC": vc_c}, cat2)
    afirmar(any("mas de una" in e for e in errs_conv),
            "B3-c una sucesora no puede suceder a dos antecesoras",
            "; ".join(e for e in errs_conv if "mas de una" in e))

    afirmar(bool(validar_configuracion(
                _v(estado="EstadoInventado", grupos=(G_LEY,)), cat2)),
            "B4-a un estado fuera del vocabulario se rechaza")
    afirmar(bool(validar_configuracion(
                _v(grupos=(G_LEY, Grupo("GX", "ROL_INVENTADO", None,
                                        ("LEY",)))), cat2)),
            "B4-b un rol fuera del vocabulario se rechaza")
    afirmar(bool(validar_configuracion(
                _v(grupos=(G_LEY, Grupo("GX", ROL_REGLAMENTA, "F", ("LEY",),
                                        (2024,)))), cat2)),
            "B4-c gestiones_objetivo en un rol que no es HABILITA se rechaza")

    v_g = _v(id="V1", grupos=(G_LEY,))
    afirmar(bool(validar_snapshot_completo(v_g, _c_res(v_g, cat2), cat2,
                                           "GATE_INVENTADO")),
            "B5-a validar_snapshot_completo rechaza un gate desconocido")
    afirmar(bool(validar_snapshot_completo(v_g, _c_res(v_g, cat2), cat2,
                                           GATE_APROBACION)),
            "B5-b APROBACION no admite snapshot: no es un gate con snapshot")
    try:
        habilitada_para_gate(v_g, _c(), "GATE_INVENTADO", cat2)
        afirmar(False, "B5-c el gate desconocido no se ejecuta en silencio")
    except ValueError:
        afirmar(True, "B5-c un gate desconocido lanza ValueError")

    vciclo = {"VA": _v(id="VA", estado="Reemplazada", sucesora_id="VB",
                       fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,)),
              "VB": _v(id="VB", estado="Reemplazada", sucesora_id="VC",
                       fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,)),
              "VC": _v(id="VC", estado="Reemplazada", sucesora_id="VA",
                       fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))}
    ciclos = [e for e in validar_modelo(vciclo, cat2) if "ciclo" in e]
    afirmar(len(ciclos) == 1,
            "B6 un ciclo de tres nodos se reporta UNA vez, no una por rotacion",
            f"{len(ciclos)} entradas: {ciclos}")

    # --- CONTRAPRUEBAS FOCALIZADAS DE LA QUINTA PASADA ---
    # El grupo formal lleva DOS alternativas aplicables: asi la
    # sustitucion es legitima para validar_corrida y el unico motivo de
    # rechazo posible es la monotonia. Con un instrumento ausente del
    # catalogo, la prueba pasaria por la razon equivocada.
    ley_alt = Instrumento("LEY_ALT", MUN, D(2024, 1, 1))
    cat_q = {"LEY": LEY, "LEY_ALT": ley_alt}
    g_alt = Grupo("G1", ROL_APRUEBA, "P1", ("LEY", "LEY_ALT"))
    v_q = _v(id="V1", grupos=(g_alt, Grupo("GH", ROL_HABILITA, "2024",
                                           ("LEY",), (2024,))))
    cq_prev = _c_res(v_q, cat_q, GATE_PREVIEW)
    cq_emi = _c_res(v_q, cat_q, GATE_EMISION)

    # Q1: duplicados en cualquiera de los dos snapshots
    cq_dup_ant = Corrida(**{**cq_prev.__dict__, "resolucion_congelada":
                            cq_prev.resolucion_congelada
                            + cq_prev.resolucion_congelada[:1]})
    afirmar(bool(validar_monotonia_snapshot(cq_dup_ant, cq_emi)),
            "Q1-a un duplicado en el snapshot ANTERIOR se detecta",
            "; ".join(validar_monotonia_snapshot(cq_dup_ant, cq_emi)))
    cq_dup_post = Corrida(**{**cq_emi.__dict__, "resolucion_congelada":
                             cq_emi.resolucion_congelada
                             + cq_emi.resolucion_congelada[:1]})
    afirmar(bool(validar_monotonia_snapshot(cq_prev, cq_dup_post)),
            "Q1-b un duplicado en el snapshot POSTERIOR se detecta")
    afirmar(not validar_monotonia_snapshot(cq_prev, cq_emi),
            "Q1-c sin duplicados y extendiendo, no hay error")

    # Q2: la monotonia esta CONECTADA al gate de emision
    try:
        habilitada_para_gate(v_q, cq_emi, GATE_EMISION, cat_q)
        afirmar(False, "Q2-a Emision exige el snapshot de PreviewListo")
    except CorridaInvalida as e:
        afirmar(True, "Q2-a Emision exige el snapshot de PreviewListo", str(e))
    cq_sust = Corrida(**{**cq_emi.__dict__, "resolucion_congelada": tuple(
        (g, "LEY_ALT" if g == "G1" else i)
        for g, i in cq_emi.resolucion_congelada)})
    afirmar(not validar_corrida(v_q, cq_sust, cat_q),
            "Q2-b0 la corrida sustituida es VALIDA salvo por la monotonia",
            "sin esto, Q2-b pasaria por la razon equivocada")
    try:
        habilitada_para_gate(v_q, cq_sust, GATE_EMISION, cat_q, None, cq_prev)
        afirmar(False, "Q2-b el gate rechaza una eleccion sustituida")
    except CorridaInvalida as e:
        afirmar("cambio su eleccion congelada" in str(e),
                "Q2-b el gate rechaza una eleccion sustituida POR MONOTONIA",
                str(e))
    afirmar(habilitada_para_gate(v_q, cq_emi, GATE_EMISION, cat_q, None,
                                 cq_prev),
            "Q2-c extendiendo el snapshot, la emision procede")

    # Q3: el registro debe ser el de ESTA version
    v_a = _v(id="VX", grupos=(G_LEY,))
    v_otra = _v(id="VY", grupos=(G_LEY,))
    afirmar(not validar_modelo({"VY": v_otra}, cat2),
            "Q3-a el registro ajeno es internamente VALIDO")
    try:
        puede_aprobar(_v(id="VX", estado="PropuestaTecnica",
                         fecha_aprobacion=None, grupos=(G_LEY,)), cat2,
                      {"VY": v_otra})
        afirmar(False, "Q3-b un registro que no contiene la version se rechaza")
    except ConfiguracionInvalida as e:
        afirmar(True, "Q3-b un registro que no contiene la version se rechaza",
                str(e))
    v_impostora = _v(id="VX", gestion_desde=2030, gestion_hasta=2030,
                     grupos=(G_LEY,))
    try:
        puede_aprobar(_v(id="VX", estado="PropuestaTecnica",
                         fecha_aprobacion=None, grupos=(G_LEY,)), cat2,
                      {"VX": v_impostora})
        afirmar(False, "Q3-c una version distinta bajo el mismo id se rechaza")
    except ConfiguracionInvalida as e:
        afirmar(True, "Q3-c una version distinta bajo el mismo id se rechaza",
                str(e))
    afirmar(puede_aprobar(_v(id="VX", estado="PropuestaTecnica",
                             fecha_aprobacion=None, grupos=(G_LEY,)), cat2,
                          {"VX": _v(id="VX", estado="PropuestaTecnica",
                                    fecha_aprobacion=None, grupos=(G_LEY,))}),
            "Q3-d con el registro correcto, procede")

    # R1: la identidad de la corrida es inmutable en la transicion.
    # Parametrizada sobre los siete campos, uno por uno.
    alterados = {
        "id": "OTRA_CORRIDA",
        "municipio": "022001",
        "parametros_version_id": "OTRA_VERSION",
        "formula_version": "RM024-CapVI",
        "gestion": 2025,
        "fecha_corte": D(2024, 7, 1),
        "iniciada_at": dt_utc(2025, 1, 1),
    }
    assert set(alterados) == set(CAMPOS_IDENTIDAD_CORRIDA), \
        "la contraprueba debe cubrir exactamente los campos declarados"
    fallidos = []
    for campo, valor in alterados.items():
        mutada = Corrida(**{**cq_emi.__dict__, campo: valor})
        if not any(campo in e for e in
                   validar_monotonia_snapshot(cq_prev, mutada)):
            fallidos.append(campo)
    afirmar(not fallidos,
            "R1 los siete campos de identidad de la corrida son inmutables",
            f"no detectados: {fallidos}" if fallidos
            else f"{len(alterados)} campos, todos detectados")

    # R1': el gate rechaza efectivamente una corrida con identidad alterada
    for campo, valor in (("formula_version", "RM024-CapVI"),
                         ("gestion", 2025), ("fecha_corte", D(2024, 7, 1)),
                         ("iniciada_at", dt_utc(2025, 1, 1))):
        cq_id = Corrida(**{**cq_emi.__dict__, campo: valor})
        try:
            habilitada_para_gate(v_q, cq_id, GATE_EMISION, cat_q, None,
                                 cq_prev)
            afirmar(False, f"R1' el gate rechaza el cambio de {campo}")
        except (CorridaInvalida, ConfiguracionInvalida):
            afirmar(True, f"R1' el gate rechaza el cambio de {campo}")

    # R1'': la variante VALIDA sigue procediendo
    afirmar(not validar_monotonia_snapshot(cq_prev, cq_emi),
            "R1'' extender solo el snapshot no altera la identidad")

    # C-17: configuracion valida pero no aplicable -> False, nunca excepcion
    v17 = _v(id="V1", gestion_desde=2030, gestion_hasta=2030, grupos=(G_LEY,))
    afirmar(vigente_para(v17, 2024, F_CORTE, cat2) is False,
            "C-17 configuracion valida no aplicable devuelve False, no excepcion")

    # QUE-3 — HABILITA_EMISION no afecta vigencia ni preview
    hab = Grupo("GH", ROL_HABILITA, "2024", ("LEY_TARDIA",), (2024,))
    vE = _v(grupos=(G_LEY, hab))
    catE = {"LEY": LEY, "LEY_TARDIA": LEY_TARDIA}
    afirmar(vigente_para(vE, 2024, F_CORTE, catE),
            "QUE-3a un HABILITA_EMISION insatisfecho NO afecta VigentePara")
    afirmar(habilitada_para_gate(vE, _c_res(vE, catE, GATE_PREVIEW),
                                 GATE_PREVIEW, catE),
            "QUE-3b un HABILITA_EMISION insatisfecho NO afecta PreviewListo")
    afirmar(not habilitada_para_gate(vE, _c_res(vE, catE, GATE_EMISION),
                                     GATE_EMISION, catE, None,
                                     _c_res(vE, catE, GATE_PREVIEW)),
            "QUE-3c un HABILITA_EMISION insatisfecho SI bloquea Emitida")

    # QUE-5 — aprobar no exige vigencia del instrumento
    vAp = _v(estado="PropuestaTecnica", fecha_aprobacion=None,
             grupos=(G_TARDIA,))
    catAp = {"LEY_TARDIA": LEY_TARDIA}
    afirmar(habilitada_para_gate(vAp, _c(), GATE_APROBACION, catAp),
            "QUE-5a se puede aprobar con instrumento definitivo aun no vigente")
    borrador = Instrumento("BORR", MUN, D(2024, 1, 1), acto_finalizado=False)
    gBorr = Grupo("G1", ROL_APRUEBA, "P1", ("BORR",))
    afirmar(not habilitada_para_gate(_v(estado="PropuestaTecnica",
                                        fecha_aprobacion=None,
                                        grupos=(gBorr,)), _c(),
                                     GATE_APROBACION, {"BORR": borrador}),
            "QUE-5b un borrador NO habilita la aprobacion")
    afirmar(not habilitada_para_gate(_v(estado="Descartada",
                                        fecha_aprobacion=None,
                                        grupos=(G_LEY,)), _c(),
                                     GATE_APROBACION, cat2),
            "FV7 una version Descartada NO puede aprobarse")
    afirmar(not habilitada_para_gate(_v(estado="Aprobada", grupos=(G_LEY,)),
                                     _c(), GATE_APROBACION, cat2),
            "FV7' una version ya Aprobada no vuelve a aprobarse")

    # --- FV1 a FV6: contrapruebas de las validaciones corregidas ---
    gA1 = Grupo("G1", ROL_APRUEBA, "P1", ("LEY",))
    vFV = _v(id="V1", grupos=(gA1,))
    afirmar(bool(validar_corrida(vFV, _c(resolucion_congelada=(
                ("G1", "LEY"), ("G1", "LEY"))), cat2)),
            "FV1 se rechaza un grupo congelado mas de una vez")

    afirmar(bool(validar_corrida(_v(id="V1", gestion_desde=2023,
                                    grupos=(G_TARDIA,)),
                                 _c(gestion=2023,
                                    resolucion_congelada=(("G1", "LEY_TARDIA"),)),
                                 {"LEY_TARDIA": LEY_TARDIA})),
            "FV2 se rechaza congelar un instrumento no aplicable en fecha_corte")

    reg = Grupo("GR", ROL_REGLAMENTA, "RM024-CapIV", ("LEY",))
    hb = Grupo("GH", ROL_HABILITA, "2024", ("LEY",), (2024,))
    vOp = _v(id="V1", grupos=(gA1, reg, hb))
    afirmar(bool(validar_snapshot_completo(
                vOp, _c(resolucion_congelada=(("G1", "LEY"),)), cat2,
                GATE_EMISION)),
            "FV3 se rechaza omitir los grupos operativos requeridos",
            "la exigencia de cobertura vive en validar_snapshot_completo")
    afirmar(not validar_snapshot_completo(
                vOp, _c(resolucion_congelada=(("G1", "LEY"), ("GR", "LEY"))),
                cat2, GATE_PREVIEW),
            "FV3'' en PreviewListo aun no se exige el grupo habilitante")
    afirmar(not validar_corrida(vOp, _c(resolucion_congelada=(
                ("G1", "LEY"), ("GR", "LEY"), ("GH", "LEY"))), cat2),
            "FV3' con todos los grupos requeridos congelados, valida")

    vA = _v(id="VA", estado="Reemplazada", sucesora_id="VB", grupos=(G_LEY,))
    vB = _v(id="VB", estado="Reemplazada", sucesora_id="VC", grupos=(G_LEY,))
    vCc = _v(id="VC", estado="Reemplazada", sucesora_id="VA", grupos=(G_LEY,))
    afirmar(bool(validar_cadena_sucesion({"VA": vA, "VB": vB, "VC": vCc})),
            "FV4 se detecta un ciclo de reemplazo de tres nodos")
    vD = _v(id="VD", estado="Reemplazada", sucesora_id="VE", grupos=(G_LEY,))
    vE2 = _v(id="VE", grupos=(G_LEY,))
    afirmar(not validar_cadena_sucesion({"VD": vD, "VE": vE2}),
            "FV4' una cadena sin ciclo no produce error")

    suc_cesada = _v(id="V2", fecha_aprobacion=D(2024, 8, 1),
                    estado="Cesada", fecha_efecto_cese=D(2024, 8, 2),
                    grupos=(G_LEY,))
    ant_mal = _v(id="V1", estado="Reemplazada", sucesora_id="V2",
                 fecha_inicio_reemplazo=fecha_inicio_reemplazo_esperada(
                     suc_cesada, cat2), grupos=(G_LEY,))
    afirmar(bool(validar_sucesion(ant_mal, suc_cesada, cat2)),
            "FV5 se rechaza una sucesora que ya ceso en la fecha de relevo")

    a1b = Instrumento("A1", MUN, D(2024, 1, 1), cese=D(2024, 3, 1))
    a2b = Instrumento("A2", MUN, D(2024, 6, 1))
    hab_hueco = Grupo("GH", ROL_HABILITA, "2024", ("A1", "A2"), (2024,))
    vHueco = _v(grupos=(G_LEY, hab_hueco))
    catHueco = {"LEY": LEY, "A1": a1b, "A2": a2b}
    afirmar(bool(validar_continuidad_grupos(vHueco, catHueco)),
            "FV6 se rechaza un grupo OPERATIVO con hueco temporal")



# ---------------------------------------------------------------------------
# Bloque 4 — Mutantes: comprobar que las pruebas DETECTAN un modelo mal
# ---------------------------------------------------------------------------
# Un conjunto de pruebas que solo pasa no demuestra nada. Aqui se introducen
# defectos deliberados en el modelo y se exige que al menos una prueba falle.
# Si un mutante sobrevive, la prueba correspondiente es vacua.

def _probe_casos() -> int:
    malos = 0
    for n, _desc, instrumentos, v, c, vig_esp, apl_esp in CASOS:
        cat = {i.id: i for i in instrumentos}
        try:
            vig = vigente_para(v, c.gestion, c.fecha_corte, cat)
            apl = aplicable_a(v, c.gestion, c, cat)
        except ConfiguracionInvalida:
            malos += 1
            continue
        if vig != vig_esp or apl != apl_esp:
            malos += 1
    return malos


def _probe_pa() -> int:
    viol = 0
    for _e, v, c, cat in producto_configuraciones():
        try:
            _, fin = ventana_formal(v, cat)
        except ConfiguracionInvalida:
            continue
        f = c.fecha_corte
        supero = ((fin is not INFINITO and f >= fin)
                  or (v.fecha_efecto_cese is not None and f >= v.fecha_efecto_cese)
                  or (v.fecha_inicio_reemplazo is not None
                      and f >= v.fecha_inicio_reemplazo))
        if supero and aplicable_a(v, c.gestion, c, cat):
            viol += 1
    return viol


def _probe_continuidad() -> bool:
    """True si la configuracion con hueco es correctamente rechazada."""
    a1 = Instrumento("A1", MUN, D(2024, 1, 1), cese=D(2024, 3, 1))
    a2 = Instrumento("A2", MUN, D(2024, 6, 1))
    g = Grupo("G1", ROL_APRUEBA, "P1", ("A1", "A2"))
    try:
        ventana_formal(_v(grupos=(g,)), {"A1": a1, "A2": a2})
        return False
    except ConfiguracionInvalida:
        return True


def _probe_que3() -> bool:
    """True si HABILITA_EMISION no contamina vigencia ni preview."""
    hab = Grupo("GH", ROL_HABILITA, "2024", ("LEY_TARDIA",), (2024,))
    vE = _v(grupos=(G_LEY, hab))
    catE = {"LEY": LEY, "LEY_TARDIA": LEY_TARDIA}
    return (vigente_para(vE, 2024, F_CORTE, catE)
            and habilitada_para_gate(vE, _c_res(vE, catE, GATE_PREVIEW),
                                     GATE_PREVIEW, catE)
            and not habilitada_para_gate(vE, _c_res(vE, catE, GATE_EMISION),
                                         GATE_EMISION, catE, None,
                                         _c_res(vE, catE, GATE_PREVIEW)))


def _probe_que5() -> bool:
    """True si aprobar no exige vigencia pero si acto finalizado."""
    ok1 = habilitada_para_gate(_v(estado="PropuestaTecnica",
                                  fecha_aprobacion=None, grupos=(G_TARDIA,)),
                               _c(), GATE_APROBACION, {"LEY_TARDIA": LEY_TARDIA})
    borr = Instrumento("BORR", MUN, D(2024, 1, 1), acto_finalizado=False)
    ok2 = not habilitada_para_gate(_v(estado="PropuestaTecnica",
                                      fecha_aprobacion=None,
                                      grupos=(Grupo("G1", ROL_APRUEBA, "P1",
                                                    ("BORR",)),)),
                                   _c(), GATE_APROBACION, {"BORR": borr})
    return ok1 and ok2


def _probe_validaciones() -> tuple:
    cat2 = {"LEY": LEY}
    gA_ = Grupo("G1", ROL_APRUEBA, "P1", ("LEY",))
    vC = _v(id="V1", grupos=(gA_,))
    ok1 = not validar_corrida(vC, _c(resolucion_congelada=(("G1", "LEY"),)), cat2)
    ok2 = bool(validar_corrida(vC, _c(municipio="022001",
                                      resolucion_congelada=(("G1", "LEY"),)), cat2))
    ok3 = bool(validar_corrida(vC, _c(), cat2))
    return (ok1, ok2, ok3)


def _probe_habilita() -> tuple:
    cat2 = {"LEY": LEY}
    hab24 = Grupo("GH", ROL_HABILITA, "2024", ("LEY",), (2024,))
    va = _v(grupos=(G_LEY, hab24))
    a = habilitada_para_gate(va, _c_res(va, cat2, gestion=2024),
                             GATE_EMISION, cat2, None,
                             _c_res(va, cat2, GATE_PREVIEW, gestion=2024))
    vb = _v(gestion_hasta=2025, grupos=(G_LEY, hab24))
    b = habilitada_para_gate(vb, _c_res(vb, cat2, gestion=2025),
                             GATE_EMISION, cat2, None,
                             _c_res(vb, cat2, GATE_PREVIEW, gestion=2025))
    vc = _v(grupos=(G_LEY,))
    c = habilitada_para_gate(vc, _c_res(vc, cat2), GATE_EMISION, cat2, None,
                             _c_res(vc, cat2, GATE_PREVIEW))
    hab_ok = Grupo("GH1", ROL_HABILITA, "2024", ("LEY",), (2024,))
    hab_no = Grupo("GH2", ROL_HABILITA, "2024", ("LEY_TARDIA",), (2024,))
    catH = {"LEY": LEY, "LEY_TARDIA": LEY_TARDIA}
    vd = _v(grupos=(G_LEY, hab_ok, hab_no))
    d = habilitada_para_gate(vd, _c_res(vd, catH, gestion=2024),
                             GATE_EMISION, catH, None,
                             _c_res(vd, catH, GATE_PREVIEW, gestion=2024))
    return (a, b, c, d)


def _probe_sucesion() -> tuple:
    cat2 = {"LEY": LEY}
    v2 = _v(id="V2", fecha_aprobacion=D(2024, 8, 1), grupos=(G_LEY,))
    relevo = fecha_inicio_reemplazo_esperada(v2, cat2)
    v1 = _v(id="V1", estado="Reemplazada", sucesora_id="V2",
            fecha_inicio_reemplazo=relevo, grupos=(G_LEY,))
    sin_errores = not validar_sucesion(v1, v2, cat2)
    solapes = sum(1 for d in range(400)
                  if vigente_para(v1, 2024, D(2024, 1, 1) + timedelta(days=d), cat2)
                  and vigente_para(v2, 2024, D(2024, 1, 1) + timedelta(days=d), cat2))
    return (sin_errores, solapes, relevo)


def _probe_falsos_verdes() -> tuple:
    cat2 = {"LEY": LEY}
    gA1 = Grupo("G1", ROL_APRUEBA, "P1", ("LEY",))
    vFV = _v(id="V1", grupos=(gA1,))
    f1 = bool(validar_corrida(vFV, _c(resolucion_congelada=(
        ("G1", "LEY"), ("G1", "LEY"))), cat2))
    f2 = bool(validar_corrida(_v(id="V1", gestion_desde=2023, grupos=(G_TARDIA,)),
                              _c(gestion=2023,
                                 resolucion_congelada=(("G1", "LEY_TARDIA"),)),
                              {"LEY_TARDIA": LEY_TARDIA}))
    reg = Grupo("GR", ROL_REGLAMENTA, "RM024-CapIV", ("LEY",))
    hb = Grupo("GH", ROL_HABILITA, "2024", ("LEY",), (2024,))
    f3 = bool(validar_snapshot_completo(
        _v(id="V1", grupos=(gA1, reg, hb)),
        _c(resolucion_congelada=(("G1", "LEY"),)), cat2, GATE_EMISION))
    vA = _v(id="VA", estado="Reemplazada", sucesora_id="VB", grupos=(G_LEY,))
    vB = _v(id="VB", estado="Reemplazada", sucesora_id="VC", grupos=(G_LEY,))
    vC = _v(id="VC", estado="Reemplazada", sucesora_id="VA", grupos=(G_LEY,))
    f4 = bool(validar_cadena_sucesion({"VA": vA, "VB": vB, "VC": vC}))
    suc = _v(id="V2", fecha_aprobacion=D(2024, 8, 1), estado="Cesada",
             fecha_efecto_cese=D(2024, 8, 2), grupos=(G_LEY,))
    ant = _v(id="V1", estado="Reemplazada", sucesora_id="V2",
             fecha_inicio_reemplazo=fecha_inicio_reemplazo_esperada(suc, cat2),
             grupos=(G_LEY,))
    f5 = bool(validar_sucesion(ant, suc, cat2))
    a1b = Instrumento("A1", MUN, D(2024, 1, 1), cese=D(2024, 3, 1))
    a2b = Instrumento("A2", MUN, D(2024, 6, 1))
    hh = Grupo("GH", ROL_HABILITA, "2024", ("A1", "A2"), (2024,))
    f6 = bool(validar_continuidad_grupos(_v(grupos=(G_LEY, hh)),
                                         {"LEY": LEY, "A1": a1b, "A2": a2b}))
    f7 = habilitada_para_gate(_v(estado="Descartada", fecha_aprobacion=None,
                                 grupos=(G_LEY,)), _c(), GATE_APROBACION, cat2)
    return (f1, f2, f3, f4, f5, f6, f7)


def _probe_composicion() -> tuple:
    cat2 = {"LEY": LEY}
    gdup1 = Grupo("GX", ROL_APRUEBA, "P1", ("LEY",))
    gdup2 = Grupo("GX", ROL_REGLAMENTA, "RM024-CapIV", ("LEY",))
    vdup = _v(grupos=(gdup1, gdup2))
    try:
        vigente_para(vdup, 2024, F_CORTE, cat2)
        a = False
    except ConfiguracionInvalida:
        a = True
    vconn = _v(id="V1", grupos=(G_LEY,))
    try:
        habilitada_para_gate(vconn, _c_res(vconn, cat2, GATE_PREVIEW,
                                           municipio="022001"),
                             GATE_PREVIEW, cat2)
        b = False
    except CorridaInvalida:
        b = True
    # M20b: aplicable_a debe cumplir su contrato por si mismo
    try:
        aplicable_a(vconn, 2024, _c(municipio="022001"), cat2)
        g_ = False
    except CorridaInvalida:
        g_ = True
    fc = Fundamento("FX", (2024,), ("NO_EXISTE",), D(2020, 1, 1))
    c_ = bool(validar_configuracion(_v(grupos=(G_LEY,), fundamentos=(fc,)), cat2))
    vpa = _v(estado="PropuestaTecnica", fecha_aprobacion=None, grupos=(G_LEY,))
    d = puede_aprobar(vpa, cat2)
    try:
        puede_aprobar(_v(grupos=(gdup1, gdup2), estado="PropuestaTecnica",
                         fecha_aprobacion=None), cat2)
        e_ = False
    except ConfiguracionInvalida:
        e_ = True
    vg1 = _v(id="VA", estado="Reemplazada", sucesora_id="VB",
             fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))
    f_ = bool(validar_modelo({"VA": vg1}, cat2))
    # el validador global no debe propagar excepciones (contrato)
    vfalta = _v(id="VZ", grupos=(Grupo("GM", ROL_APRUEBA, "P1", ("FALTA",)),))
    try:
        h_ = bool(validar_modelo({"VZ": vfalta}, {}))
    except Exception:
        h_ = "EXCEPCION"
    v15 = _v(id="V1", grupos=(G_LEY,))
    try:
        puede_marcar_preview_listo(v15, _c(), cat2)
        i_ = False
    except CorridaInvalida:
        i_ = True
    # monotonia
    vm = _v(id="V1", grupos=(G_LEY, Grupo("GH", ROL_HABILITA, "2024",
                                          ("LEY",), (2024,))))
    cp = _c_res(vm, cat2, GATE_PREVIEW)
    ce = _c_res(vm, cat2, GATE_EMISION)
    ce_mal = Corrida(**{**ce.__dict__, "resolucion_congelada": tuple(
        (g, "LEY_ALT" if g == "G1" else i) for g, i in ce.resolucion_congelada)})
    j_ = (not validar_monotonia_snapshot(cp, ce),
          bool(validar_monotonia_snapshot(cp, ce_mal)))
    # modelo global
    vcolg = _v(id="VA", estado="Reemplazada", sucesora_id="NO_EXISTE",
               fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))
    try:
        habilitada_para_gate(vcolg, _c_res(vcolg, cat2, GATE_PREVIEW),
                             GATE_PREVIEW, cat2)
        k_ = False
    except ConfiguracionInvalida:
        k_ = True
    # registro
    vok = _v(id="V1", grupos=(G_LEY,))
    l_ = (bool(validar_modelo({"OTRA": vok}, cat2)),
          bool(validar_modelo({"V1": vok, "V1b": _v(id="V1", grupos=(G_LEY,))},
                              cat2)))
    va_c = _v(id="VA", estado="Reemplazada", sucesora_id="VC",
              fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))
    vb_c = _v(id="VB", estado="Reemplazada", sucesora_id="VC",
              fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))
    m_ = any("mas de una" in e for e in validar_modelo(
        {"VA": va_c, "VB": vb_c, "VC": _v(id="VC", grupos=(G_LEY,))}, cat2))
    # vocabulario
    n_ = (bool(validar_configuracion(_v(estado="XX", grupos=(G_LEY,)), cat2)),
          bool(validar_configuracion(_v(grupos=(G_LEY, Grupo("GX", "RX", None,
                                                             ("LEY",)))), cat2)))
    o_ = bool(validar_snapshot_completo(vok, _c_res(vok, cat2), cat2, "XX"))
    vcic = {"VA": va_c, "VB": _v(id="VB", estado="Reemplazada", sucesora_id="VA",
                                 fecha_inicio_reemplazo=D(2024, 9, 1),
                                 grupos=(G_LEY,)),
            "VC": _v(id="VC", estado="Reemplazada", sucesora_id="VB",
                     fecha_inicio_reemplazo=D(2024, 9, 1), grupos=(G_LEY,))}
    p_ = len([e for e in validar_modelo(vcic, cat2) if "ciclo" in e])
    # quinta pasada
    ley_alt = Instrumento("LEY_ALT", MUN, D(2024, 1, 1))
    cat_q = {"LEY": LEY, "LEY_ALT": ley_alt}
    vq = _v(id="V1", grupos=(Grupo("G1", ROL_APRUEBA, "P1",
                                   ("LEY", "LEY_ALT")),
                             Grupo("GH", ROL_HABILITA, "2024",
                                   ("LEY",), (2024,))))
    cqp = _c_res(vq, cat_q, GATE_PREVIEW)
    cqe = _c_res(vq, cat_q, GATE_EMISION)
    cdup = Corrida(**{**cqp.__dict__, "resolucion_congelada":
                      cqp.resolucion_congelada + cqp.resolucion_congelada[:1]})
    q1 = bool(validar_monotonia_snapshot(cdup, cqe))
    try:
        habilitada_para_gate(vq, cqe, GATE_EMISION, cat_q)
        q2 = False
    except CorridaInvalida:
        q2 = True
    csust = Corrida(**{**cqe.__dict__, "resolucion_congelada": tuple(
        (g, "LEY_ALT" if g == "G1" else i)
        for g, i in cqe.resolucion_congelada)})
    try:
        habilitada_para_gate(vq, csust, GATE_EMISION, cat_q, None, cqp)
        q3 = False
    except CorridaInvalida as exc:
        q3 = "cambio su eleccion congelada" in str(exc)
    try:
        puede_aprobar(_v(id="VX", estado="PropuestaTecnica",
                         fecha_aprobacion=None, grupos=(G_LEY,)), cat2,
                      {"VY": _v(id="VY", grupos=(G_LEY,))})
        q4 = False
    except ConfiguracionInvalida:
        q4 = True
    r1 = tuple(
        bool(validar_monotonia_snapshot(
            cqp, Corrida(**{**cqe.__dict__, campo: valor})))
        for campo, valor in (("formula_version", "RM024-CapVI"),
                             ("gestion", 2025),
                             ("fecha_corte", D(2024, 7, 1)),
                             ("iniciada_at", dt_utc(2025, 1, 1))))
    return (a, b, c_, d, e_, f_, g_, h_, i_, j_, k_, l_, m_, n_, o_, p_,
            q1, q2, q3, q4, r1)


def _seguro(fn):
    """Una sonda que explota bajo mutacion tambien delata al mutante.
    Se captura para que el resultado sea comparable, no para ocultarlo."""
    try:
        return fn()
    except Exception as e:
        return f"EXCEPCION:{type(e).__name__}"


def _estado_global() -> tuple:
    return tuple(_seguro(f) for f in (
        _probe_casos, _probe_pa, _probe_continuidad, _probe_que3,
        _probe_que5, _probe_validaciones, _probe_habilita, _probe_sucesion,
        _probe_falsos_verdes, _probe_composicion))


def _estado_global_viejo() -> tuple:
    return (_probe_casos(), _probe_pa(), _probe_continuidad(),
            _probe_que3(), _probe_que5(), _probe_validaciones(),
            _probe_habilita(), _probe_sucesion(), _probe_falsos_verdes(),
            _probe_composicion())


SANO = None


def _detecta(mutado: tuple) -> bool:
    """True si el estado mutado difiere del sano: la prueba lo detecto."""
    return mutado != SANO


def correr_mutantes() -> None:
    global SANO
    M = sys.modules[__name__]
    seccion("BLOQUE 4 — MUTANTES: LAS PRUEBAS, ¿DETECTARIAN UN MODELO MAL?")
    SANO = _estado_global()
    sondas_rotas = [i for i, x in enumerate(SANO)
                    if isinstance(x, str) and x.startswith("EXCEPCION:")]
    afirmar(not sondas_rotas,
            "GUARDA ninguna sonda lanza excepcion en la linea base sana",
            f"sondas rotas en las posiciones {sondas_rotas}: su casilla "
            "seria constante y no detectaria ningun mutante"
            if sondas_rotas else f"{len(SANO)} sondas sanas")
    print(f"  linea base sana: casos_malos={SANO[0]} viol_PA={SANO[1]} "
          f"continuidad={SANO[2]} que3={SANO[3]} que5={SANO[4]} "
          f"valid={SANO[5]} habilita={SANO[6]} sucesion={SANO[7]}")
    print()

    # M1 — la excepcion tambien levanta el extremo SUPERIOR
    orig = M.aplicable_a

    def m1(v, gestion, c, cat):
        if vigente_para(v, gestion, c.fecha_corte, cat):
            return True
        f = c.fecha_corte
        if not _base_ok(v, gestion, f):
            return False
        if v.fecha_aprobacion >= fecha_local(c.iniciada_at):
            return False
        for fund in v.fundamentos:
            if gestion in fund.gestiones:
                return True          # DEFECTO: ignora todos los limites
        return False

    M.aplicable_a = m1
    afirmar(_detecta(_estado_global()), "M1 detectado",
            "la excepcion que levanta el extremo superior")
    M.aplicable_a = orig

    # M2 — comparacion de aprobacion no estricta
    orig_base = M._base_ok

    def m2(v, gestion, f):
        if not cubre_gestion(v, gestion):
            return False
        if v.estado not in ESTADOS_CON_EFECTO_HISTORICO:
            return False
        if v.fecha_aprobacion is None or not (v.fecha_aprobacion <= f):
            return False             # DEFECTO: <= en vez de <
        return _limites_propios_ok(v, f)

    M._base_ok = m2
    afirmar(_detecta(_estado_global()), "M2 detectado",
            "aprobacion del mismo dia aceptada")
    M._base_ok = orig_base

    # M3 — se admiten huecos entre alternativas
    orig_union = M.union_continua

    def m3(ventanas):
        if not ventanas:
            raise ConfiguracionInvalida("grupo requerido sin alternativas")
        inicio = min(v[0] for v in ventanas)
        fin = None
        for _, f in ventanas:
            fin = _fin_mayor(fin, f) if fin is not None else f
        return (inicio, fin)         # DEFECTO: no verifica continuidad

    M.union_continua = m3
    afirmar(_detecta(_estado_global()), "M3 detectado",
            "huecos entre alternativas admitidos")
    M.union_continua = orig_union

    # M4 — HABILITA_EMISION contamina la vigencia formal
    orig_gf = M.grupos_formales

    def m4(v):
        return [g for g in v.grupos
                if g.rol in (ROL_APRUEBA, ROL_HABILITA)]   # DEFECTO

    M.grupos_formales = m4
    afirmar(_detecta(_estado_global()), "M4 detectado",
            "HABILITA_EMISION incluido en la vigencia formal")
    M.grupos_formales = orig_gf

    # M5 — aprobar exige que el instrumento ya este vigente
    orig_gate = M.habilitada_para_gate

    def m5(v, c, gate, cat, modelo=None, corrida_preview=None):
        if gate == GATE_APROBACION:
            if v.estado != "PropuestaTecnica":
                return False
            gs = grupos_formales(v)
            if not gs:
                return False
            for g in gs:
                if not any(cat[i].acto_finalizado
                           and cat[i].entrada_vigencia <= c.fecha_corte
                           for i in g.alternativas):
                    return False     # DEFECTO: mezcla aprobacion con vigencia
            return True
        return orig_gate(v, c, gate, cat, modelo, corrida_preview)

    M.habilitada_para_gate = m5
    afirmar(_detecta(_estado_global()), "M5 detectado",
            "aprobacion condicionada a vigencia del instrumento")
    M.habilitada_para_gate = orig_gate

    # M6 — el defecto real detectado por el orquestador: la rama excepcional
    # exige fecha_aprobacion < fecha_corte, con lo que la retroactividad
    # queda bloqueada justo para el caso que esa rama existe para cubrir.
    orig_apl = M.aplicable_a

    def m6(v, gestion, c, cat):
        if vigente_para(v, gestion, c.fecha_corte, cat):
            return True
        f = c.fecha_corte
        if not _base_ok(v, gestion, f):      # DEFECTO: _base_ok en vez de comun
            return False
        _, fin = ventana_formal(v, cat)
        if fin is not INFINITO and f >= fin:
            return False
        if not (v.fecha_aprobacion < fecha_local(c.iniciada_at)):
            return False
        for fund in v.fundamentos:
            if gestion in fund.gestiones and f >= fund.fecha_aplicacion_desde:
                if all(grupo_satisfecho(g, f, cat, fund)
                       for g in grupos_formales(v)):
                    return True
        return False

    M.aplicable_a = m6
    afirmar(_detecta(_estado_global()), "M6 detectado",
            "la rama excepcional bloqueada para la retroactividad genuina")
    M.aplicable_a = orig_apl

    # M7 — se ignora fecha_aplicacion_desde del fundamento
    def m7(v, gestion, c, cat):
        if vigente_para(v, gestion, c.fecha_corte, cat):
            return True
        f = c.fecha_corte
        if not _base_comun(v, gestion, f):
            return False
        _, fin = ventana_formal(v, cat)
        if fin is not INFINITO and f >= fin:
            return False
        if not (v.fecha_aprobacion < fecha_local(c.iniciada_at)):
            return False
        for fund in v.fundamentos:
            if gestion in fund.gestiones:    # DEFECTO: sin fecha_aplicacion_desde
                if all(grupo_satisfecho(g, f, cat, fund)
                       for g in grupos_formales(v)):
                    return True
        return False

    M.aplicable_a = m7
    afirmar(_detecta(_estado_global()), "M7 detectado",
            "fecha_aplicacion_desde ignorada")
    M.aplicable_a = orig_apl

    # M8 — HABILITA_EMISION ignora su objetivo de gestion
    orig_gate2 = M.habilitada_para_gate

    def m8(v, c, gate, cat, modelo=None, corrida_preview=None):
        if gate == GATE_EMISION:
            if not orig_gate2(v, c, GATE_PREVIEW, cat, modelo):
                return False
            for g in v.grupos:               # DEFECTO: no filtra por gestion
                if g.rol == ROL_HABILITA and not grupo_satisfecho(
                        g, c.fecha_corte, cat):
                    return False
            return True
        return orig_gate2(v, c, gate, cat, modelo, corrida_preview)

    M.habilitada_para_gate = m8
    afirmar(_detecta(_estado_global()), "M8 detectado",
            "HABILITA_EMISION sin objetivo de gestion")
    M.habilitada_para_gate = orig_gate2

    # M9 — validar_corrida no comprueba el municipio
    orig_val = M.validar_corrida

    def m9(v, c, cat):
        return [e for e in orig_val(v, c, cat) if "municipio" not in e]

    M.validar_corrida = m9
    afirmar(_detecta(_estado_global()), "M9 detectado",
            "validacion de municipio ausente")
    M.validar_corrida = orig_val

    # M10 — la fecha de relevo se calcula sin el +1 dia de la aprobacion
    orig_rel = M.fecha_inicio_reemplazo_esperada

    def m10(suc, cat):
        return inicio_normativo(suc, cat)    # DEFECTO: ignora la aprobacion

    M.fecha_inicio_reemplazo_esperada = m10
    afirmar(_detecta(_estado_global()), "M10 detectado",
            "relevo sin el +1 dia de la aprobacion")
    M.fecha_inicio_reemplazo_esperada = orig_rel

    # M11 — los grupos habilitantes se combinan con OR en vez de AND
    orig_gate3 = M.habilitada_para_gate

    def m11(v, c, gate, cat, modelo=None, corrida_preview=None):
        if gate == GATE_EMISION:
            if not orig_gate3(v, c, GATE_PREVIEW, cat, modelo):
                return False
            habilitantes = [g for g in v.grupos
                            if g.rol == ROL_HABILITA
                            and c.gestion in g.gestiones_objetivo]
            if not habilitantes:
                return False
            return any(grupo_satisfecho(g, c.fecha_corte, cat)
                       for g in habilitantes)   # DEFECTO: any en vez de all
        return orig_gate3(v, c, gate, cat, modelo, corrida_preview)

    M.habilitada_para_gate = m11
    afirmar(_detecta(_estado_global()), "M11 detectado",
            "grupos habilitantes combinados con OR")
    M.habilitada_para_gate = orig_gate3

    # --- mutantes de los siete falsos verdes del dictamen externo ---
    orig_vc = M.validar_corrida
    orig_grc = M.grupos_requeridos_por_corrida
    orig_cad = M.validar_cadena_sucesion
    orig_vs = M.validar_sucesion
    orig_cont = M.validar_continuidad_grupos
    orig_g4 = M.habilitada_para_gate

    # M12 — dict() colapsa duplicados sin avisar (FV1)
    def m12(v, c, cat):
        return [e for e in orig_vc(v, c, cat) if "mas de una vez" not in e]

    M.validar_corrida = m12
    afirmar(_detecta(_estado_global()), "M12 detectado",
            "un grupo congelado dos veces pasa inadvertido")
    M.validar_corrida = orig_vc

    # M13 — no se verifica la aplicabilidad temporal del congelado (FV2)
    def m13(v, c, cat):
        return [e for e in orig_vc(v, c, cat) if "no es aplicable en" not in e]

    M.validar_corrida = m13
    afirmar(_detecta(_estado_global()), "M13 detectado",
            "instrumento congelado no aplicable en fecha_corte")
    M.validar_corrida = orig_vc

    # M14 — solo se exigen los grupos formales (FV3)
    def m14(v, c, gate=GATE_EMISION):
        # Firma IDENTICA a la original: un mutante que cambia la firma
        # muere por TypeError y no demuestra la propiedad que anuncia.
        return list(grupos_formales(v))

    M.grupos_requeridos_por_corrida = m14
    afirmar(_detecta(_estado_global()), "M14 detectado",
            "grupos operativos no congelados")
    M.grupos_requeridos_por_corrida = orig_grc

    # M15 — deteccion de ciclos limitada a dos nodos (FV4)
    def m15(versiones):
        errs = []
        for k, v in versiones.items():
            s2 = v.sucesora_id
            if s2 in versiones and versiones[s2].sucesora_id == k:
                errs.append(f"ciclo directo {k} -> {s2}")
        return sorted(set(errs))

    M.validar_cadena_sucesion = m15
    afirmar(_detecta(_estado_global()), "M15 detectado",
            "ciclo de tres nodos no detectado")
    M.validar_cadena_sucesion = orig_cad

    # M16 — no se verifica que la sucesora produzca efectos (FV5)
    def m16(ant, suc, cat):
        return [e for e in orig_vs(ant, suc, cat)
                if "ceso antes de la fecha de relevo" not in e
                and "no cubre la fecha de relevo" not in e
                and "reemplazada antes de la fecha" not in e]

    M.validar_sucesion = m16
    afirmar(_detecta(_estado_global()), "M16 detectado",
            "sucesora incapaz de producir efectos aceptada")
    M.validar_sucesion = orig_vs

    # M17 — continuidad solo en los grupos formales (FV6)
    def m17(v, cat):
        errs = []
        for g in grupos_formales(v):
            if not g.alternativas:
                errs.append(f"grupo {g.id} sin alternativas")
                continue
            try:
                union_continua([cat[i].ventana() for i in g.alternativas])
            except ConfiguracionInvalida as e:
                errs.append(f"grupo {g.id}: {e}")
        return errs

    M.validar_continuidad_grupos = m17
    afirmar(_detecta(_estado_global()), "M17 detectado",
            "hueco en un grupo operativo admitido")
    M.validar_continuidad_grupos = orig_cont

    # M18 — GATE_APROBACION sin comprobar el estado de partida (FV7)
    def m18(v, c, gate, cat, modelo=None, corrida_preview=None):
        if gate == GATE_APROBACION:
            gs = grupos_formales(v)
            if not gs:
                return False
            return all(any(cat[i].acto_finalizado for i in g.alternativas)
                       for g in gs)
        return orig_g4(v, c, gate, cat, modelo, corrida_preview)

    M.habilitada_para_gate = m18
    afirmar(_detecta(_estado_global()), "M18 detectado",
            "una version Descartada podria aprobarse")
    M.habilitada_para_gate = orig_g4

    # --- mutantes de COMPOSICION: omitir cada llamada entre capas ---
    orig_vp = M.vigente_para
    orig_ap = M.aplicable_a
    orig_hg = M.habilitada_para_gate
    orig_pa = M.puede_aprobar
    orig_cfg = M.validar_configuracion

    # M19 — vigente_para no valida la configuracion
    def m19(v, gestion, f, cat):
        if not _base_ok(v, gestion, f):
            return False
        return contiene(ventana_formal(v, cat), f)

    M.vigente_para = m19
    afirmar(_detecta(_estado_global()), "M19 detectado",
            "vigente_para desconectado de validar_configuracion")
    M.vigente_para = orig_vp

    # M20 — se anula _exigir_corrida, que es la ruta 1 (gate) y la 2
    # (aplicable_a). NO anula la ruta 3: validar_snapshot_completo llama
    # a validar_corrida directamente y no pasa por _exigir_corrida.
    orig_ec = M._exigir_corrida

    def m20(v, c, cat):
        return None                        # DEFECTO: validacion anulada

    M._exigir_corrida = m20
    afirmar(_detecta(_estado_global()), "M20 detectado",
            "anulado _exigir_corrida: caen las rutas 1 y 2, no la 3")
    M._exigir_corrida = orig_ec

    # CTRL-REDUNDANCIA — no es un mutante: es un CONTROL. Quitar la
    # validacion solo del gate NO debe cambiar nada, porque quedan
    # aplicable_a y validar_snapshot_completo. Se etiqueta sin la letra M
    # para que el conteo de mutantes sea inequivoco.
    base_antes = _estado_global()

    def m20b(v, c, gate, cat, modelo=None, corrida_preview=None):
        # Copia EXACTA del cuerpo actual de GATE_PREVIEW, menos la llamada
        # a _exigir_corrida. Si no es copia exacta, mide otra cosa.
        if gate == GATE_PREVIEW:
            _exigir_configuracion(v, cat)
            _exigir_modelo(v, cat, modelo)
            if not aplicable_a(v, c.gestion, c, cat):
                return False
            for g in v.grupos:
                if g.rol == ROL_REGLAMENTA and g.objetivo == c.formula_version:
                    if not grupo_satisfecho(g, c.fecha_corte, cat):
                        return False
            errs = validar_snapshot_completo(v, c, cat, GATE_PREVIEW)
            if errs:
                raise CorridaInvalida("; ".join(errs))
            return True
        return orig_hg(v, c, gate, cat, modelo, corrida_preview)

    M.habilitada_para_gate = m20b
    afirmar(not _detecta(_estado_global()),
            "CTRL-REDUNDANCIA quitar la validacion SOLO del gate no cambia "
            "el resultado",
            "quedan aplicable_a (ruta 2) y validar_snapshot_completo (ruta 3): "
            "la redundancia no es atribuible a una sola de ellas")
    M.habilitada_para_gate = orig_hg

    # M21 — puede_aprobar no valida la configuracion
    def m21(v, cat):
        if v.estado != "PropuestaTecnica":
            return False
        gs = grupos_formales(v)
        return bool(gs) and all(
            any(cat[i].acto_finalizado for i in g.alternativas) for g in gs)

    M.puede_aprobar = m21
    afirmar(_detecta(_estado_global()), "M21 detectado",
            "puede_aprobar desconectado de validar_configuracion")
    M.puede_aprobar = orig_pa

    # M22 — validar_configuracion no comprueba la unicidad de grupo.id
    def m22(v, cat):
        return [e for e in orig_cfg(v, cat) if "duplicado" not in e]

    M.validar_configuracion = m22
    afirmar(_detecta(_estado_global()), "M22 detectado",
            "grupo.id duplicado admitido")
    M.validar_configuracion = orig_cfg

    # M23 — no se comprueban las referencias colgantes del fundamento
    def m23(v, cat):
        return [e for e in orig_cfg(v, cat)
                if "inexistente" not in e and "improcedente" not in e]

    M.validar_configuracion = m23
    afirmar(_detecta(_estado_global()), "M23 detectado",
            "fundamento con referencia colgante admitido")
    M.validar_configuracion = orig_cfg

    # M24 — configuracion invalida devuelve False en vez de excepcion
    def m24(v, cat):
        return []

    M.validar_configuracion = m24
    afirmar(_detecta(_estado_global()), "M24 detectado",
            "toda configuracion se considera valida")
    M.validar_configuracion = orig_cfg

    # M25 — quitar la validacion de corrida SOLO de aplicable_a.
    # El gate debe seguir protegido, pero una llamada directa a
    # aplicable_a deja de cumplir su contrato.
    orig_ap2 = M.aplicable_a

    def m25(v, gestion, c, cat):
        _exigir_configuracion(v, cat)          # DEFECTO: sin _exigir_corrida
        if vigente_para(v, gestion, c.fecha_corte, cat):
            return True
        f = c.fecha_corte
        if not _base_comun(v, gestion, f):
            return False
        _, fin = ventana_formal(v, cat)
        if fin is not INFINITO and f >= fin:
            return False
        if not (v.fecha_aprobacion < fecha_local(c.iniciada_at)):
            return False
        for fund in v.fundamentos:
            if gestion in fund.gestiones and f >= fund.fecha_aplicacion_desde:
                if all(grupo_satisfecho(g, f, cat, fund)
                       for g in grupos_formales(v)):
                    return True
        return False

    M.aplicable_a = m25
    afirmar(_detecta(_estado_global()), "M25 detectado",
            "aplicable_a deja de validar la corrida por si mismo")
    M.aplicable_a = orig_ap2

    # M26 — validar_modelo compone aristas sin comprobar los extremos
    orig_vm = M.validar_modelo

    def m26(versiones, cat):
        errs = []
        for vid, v in versiones.items():
            errs += [f"{vid}: {e}" for e in validar_configuracion(v, cat)]
            if v.sucesora_id in versiones:
                errs += validar_sucesion(v, versiones[v.sucesora_id], cat)
        return sorted(set(errs))               # DEFECTO: propaga KeyError

    M.validar_modelo = m26
    afirmar(_detecta(_estado_global()), "M26 detectado",
            "validar_modelo propaga excepciones en vez de errores")
    M.validar_modelo = orig_vm

    # M27 — PreviewListo no exige snapshot congelado
    orig_pml = M.puede_marcar_preview_listo

    def m27(v, c, cat):
        _exigir_configuracion(v, cat)
        _exigir_corrida(v, c, cat)
        return aplicable_a(v, c.gestion, c, cat)   # DEFECTO: sin snapshot

    M.puede_marcar_preview_listo = m27
    afirmar(_detecta(_estado_global()), "M27 detectado",
            "PreviewListo alcanzable sin snapshot congelado")
    M.puede_marcar_preview_listo = orig_pml

    # --- mutantes de los cinco bloqueantes de la cuarta pasada ---
    orig_mono = M.validar_monotonia_snapshot
    orig_em = M._exigir_modelo
    orig_vm2 = M.validar_modelo
    orig_cfg2 = M.validar_configuracion
    orig_vsc = M.validar_snapshot_completo
    orig_cad2 = M.validar_cadena_sucesion

    # M28 — la monotonia solo comprueba que el snapshot crezca en tamano
    def m28(anterior, posterior):
        if len(posterior.resolucion_congelada) < len(
                anterior.resolucion_congelada):
            return ["el snapshot se encogio"]
        return []                              # DEFECTO: no compara elecciones

    M.validar_monotonia_snapshot = m28
    afirmar(_detecta(_estado_global()), "M28 detectado",
            "una eleccion congelada puede cambiar sin ser detectada")
    M.validar_monotonia_snapshot = orig_mono

    # M29 — el flujo operativo no exige modelo global valido
    def m29(v, cat, modelo):
        return None                            # DEFECTO: sin validacion global

    M._exigir_modelo = m29
    afirmar(_detecta(_estado_global()), "M29 detectado",
            "los gates operan dentro de un registro roto")
    M._exigir_modelo = orig_em

    # M30 — el registro no comprueba clave/id ni unicidad
    def m30(versiones, cat):
        return [e for e in orig_vm2(versiones, cat)
                if "clave del registro" not in e
                and "id de version duplicado" not in e]

    M.validar_modelo = m30
    afirmar(_detecta(_estado_global()), "M30 detectado",
            "clave distinta del id, o id duplicado, admitidos")
    M.validar_modelo = orig_vm2

    # M31 — convergencia de sucesoras admitida
    def m31(versiones, cat):
        return [e for e in orig_vm2(versiones, cat) if "mas de una" not in e]

    M.validar_modelo = m31
    afirmar(_detecta(_estado_global()), "M31 detectado",
            "dos antecesoras convergiendo en una sucesora")
    M.validar_modelo = orig_vm2

    # M32 — vocabulario abierto de estados y roles
    def m32(v, cat):
        return [e for e in orig_cfg2(v, cat)
                if "desconocido" not in e and "solo aplica a" not in e]

    M.validar_configuracion = m32
    afirmar(_detecta(_estado_global()), "M32 detectado",
            "estados y roles fuera del vocabulario admitidos")
    M.validar_configuracion = orig_cfg2

    # M33 — gate desconocido aceptado por el snapshot
    def m33(v, c, cat, gate=GATE_PREVIEW):
        return [e for e in orig_vsc(v, c, cat, gate)
                if "gate desconocido" not in e]

    M.validar_snapshot_completo = m33
    afirmar(_detecta(_estado_global()), "M33 detectado",
            "gate desconocido admitido en el snapshot")
    M.validar_snapshot_completo = orig_vsc

    # M34 — ciclos reportados una vez por rotacion
    def m34(versiones):
        errs = []
        for inicio in versiones:
            visto = []
            actual = inicio
            while actual is not None and actual in versiones:
                if actual in visto:
                    errs.append("ciclo de reemplazo: " + " -> ".join(
                        visto[visto.index(actual):] + [actual]))
                    break
                visto.append(actual)
                actual = versiones[actual].sucesora_id
        return sorted(set(errs))               # DEFECTO: una por rotacion

    M.validar_cadena_sucesion = m34
    afirmar(_detecta(_estado_global()), "M34 detectado",
            "un mismo ciclo reportado una vez por rotacion")
    M.validar_cadena_sucesion = orig_cad2

    # --- mutantes de los tres contratos de la quinta pasada ---
    orig_mono2 = M.validar_monotonia_snapshot
    orig_em2 = M._exigir_modelo
    orig_hg2 = M.habilitada_para_gate

    # M35 — la monotonia vuelve a colapsar duplicados
    def m35(anterior, posterior):
        errs = []
        if anterior.id != posterior.id:
            errs.append("no son la misma corrida")
        prev = dict(anterior.resolucion_congelada)   # DEFECTO: sin duplicados
        post = dict(posterior.resolucion_congelada)
        for gid, iid in prev.items():
            if gid not in post:
                errs.append(f"el grupo {gid} desaparecio del snapshot")
            elif post[gid] != iid:
                errs.append(f"el grupo {gid} cambio su eleccion congelada: "
                            f"{iid} -> {post[gid]}")
        return errs

    M.validar_monotonia_snapshot = m35
    afirmar(_detecta(_estado_global()), "M35 detectado",
            "duplicados colapsados en la monotonia")
    M.validar_monotonia_snapshot = orig_mono2

    # M36 — el registro no tiene que contener la version evaluada
    def m36(v, cat, modelo):
        reg = modelo if modelo is not None else {v.id: v}
        errs = validar_modelo(reg, cat)          # DEFECTO: sin comprobar v
        if errs:
            raise ConfiguracionInvalida("modelo global invalido: "
                                        + "; ".join(errs))

    M._exigir_modelo = m36
    afirmar(_detecta(_estado_global()), "M36 detectado",
            "un registro ajeno a la version evaluada se acepta")
    M._exigir_modelo = orig_em2

    # M37 — la emision no exige el snapshot de Preview
    def m37(v, c, gate, cat, modelo=None, corrida_preview=None):
        if gate == GATE_EMISION:
            return orig_hg2(v, c, gate, cat, modelo, c)   # DEFECTO: c consigo
        return orig_hg2(v, c, gate, cat, modelo, corrida_preview)

    M.habilitada_para_gate = m37
    afirmar(_detecta(_estado_global()), "M37 detectado",
            "la emision se compara consigo misma: monotonia trivial")
    M.habilitada_para_gate = orig_hg2

    # M38 — la monotonia identifica la corrida SOLO por su id
    orig_mono3 = M.validar_monotonia_snapshot

    def m38(anterior, posterior):
        errs = []
        if anterior.id != posterior.id:      # DEFECTO: solo el id
            errs.append("no son la misma corrida")
        for etiqueta, c in (("anterior", anterior), ("posterior", posterior)):
            vistos = set()
            for gid, _ in c.resolucion_congelada:
                if gid in vistos:
                    errs.append(f"snapshot {etiqueta}: grupo {gid} congelado "
                                "mas de una vez")
                vistos.add(gid)
        if errs:
            return errs
        prev = dict(anterior.resolucion_congelada)
        post = dict(posterior.resolucion_congelada)
        for gid, iid in prev.items():
            if gid not in post:
                errs.append(f"el grupo {gid} desaparecio del snapshot")
            elif post[gid] != iid:
                errs.append(f"el grupo {gid} cambio su eleccion congelada: "
                            f"{iid} -> {post[gid]}")
        return errs

    M.validar_monotonia_snapshot = m38
    afirmar(_detecta(_estado_global()), "M38 detectado",
            "la corrida cambia formula, gestion o fecha conservando su id")
    M.validar_monotonia_snapshot = orig_mono3

    # control: sin mutacion, el estado vuelve a la linea base
    afirmar(_estado_global() == SANO, "el modelo vuelve a su estado sano",
            "control de que las mutaciones se revirtieron")


def main() -> int:
    print("MODELO EJECUTABLE DE REFERENCIA — ADR-0071, predicados temporales")
    print("Solo biblioteca estandar. Sin base de datos. Sin codigo productivo.")
    correr_casos()
    correr_propiedades_producto()
    correr_propiedades_estructurales()
    correr_mutantes()
    seccion("RESULTADO")
    if FALLAS:
        print(f"  CONTRADICCIONES DETECTADAS: {len(FALLAS)}")
        for f in FALLAS:
            print(f"    - {f}")
        return 1
    print("  Sin contradicciones internas detectadas.")
    print("  ALCANCE: este resultado NO valida juridicamente nada. El modelo")
    print("  falsa coherencia; no determina legalidad ni prevalece sobre el ADR.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
