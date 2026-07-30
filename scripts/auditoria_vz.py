#!/usr/bin/env python3
"""
Auditoria reproducible del Vz de Uyuni. SOLO LECTURA.

Reproduce, desde los archivos fuente, todas las cifras que se pretenden citar en
ADR 0066. Cada regla de admision o descarte queda registrada fila por fila.

No escribe archivos. No consulta la base. No usa listados transcritos a mano.

Uso:
    python auditoria_vz.py --emit-sql
        Imprime la consulta SQL cuya salida se necesita como tercer insumo.

    python auditoria_vz.py --encuestas <ruta> --crosswalk <ruta> --predios <ruta> [--zona C]

Insumos:
  --encuestas  CSV original de encuestas, separador ';', encoding utf-8-sig.
  --crosswalk  crosswalk_encuesta_predio.csv, separador ',', encoding utf-8-sig.
               ATENCION: es un derivado de resolver_encuestas.py, que aplica
               TOLERANCIA_ABS=10.0 m2 y EMPATE_MAX=0.5 m2. Los resultados de
               esta auditoria heredan esas dos constantes.
  --predios    salida de --emit-sql, un registro por linea, campos separados
               por '|'. Lineas que no calcen con el patron se ignoran y se
               reportan.
"""
import argparse, collections, csv, re, sys, statistics as st

SQL = (
    "SELECT p.cod_uv || '|' || p.cod_man || '|' || p.cod_pred || '|' || "
    "p.superficie_sig || '|' || z.nombre_zona "
    "FROM dominio.predios p "
    "JOIN dominio.capa_zonas z ON ST_Contains(z.geometria, ST_Centroid(p.geometria)) "
    "WHERE p.municipio_codigo='051201' AND z.dataset_version_id = "
    "(SELECT id FROM dominio.dataset_versiones WHERE municipio_codigo='051201' "
    "AND estado='Activa')"
)

R_DUDOSO = re.compile(r'\s*\(\?\)\s*$')
R_PREDIOS = re.compile(r'^\s*(\d+)\|(\d+)\|(\d+)\|([0-9.]+)\|([A-Za-z]+)\s*$')


def num(s):
    """Convierte a float. Devuelve (valor, nota). Nota != '' documenta la limpieza."""
    if s is None:
        return None, 'ausente'
    t = s.strip()
    if t == '':
        return None, 'vacio'
    nota = ''
    if R_DUDOSO.search(t):
        t = R_DUDOSO.sub('', t)
        nota = 'marcador_(?)_removido'
    if t.upper() == 'ILEGIBLE':
        return None, 'ILEGIBLE'
    try:
        return float(t), nota
    except ValueError:
        return None, f'no_numerico:{s.strip()!r}'


def sep(t):
    print()
    print('=' * 78)
    print(t)
    print('=' * 78)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--emit-sql', action='store_true')
    ap.add_argument('--encuestas')
    ap.add_argument('--crosswalk')
    ap.add_argument('--predios')
    ap.add_argument('--zona', default='C')
    a = ap.parse_args()

    if a.emit_sql:
        print(SQL)
        return 0
    if not (a.encuestas and a.crosswalk and a.predios):
        ap.error('faltan --encuestas, --crosswalk y/o --predios')

    sep('0. INSUMOS Y ADVERTENCIA DE PROCEDENCIA')
    print(f'  encuestas : {a.encuestas}')
    print(f'  crosswalk : {a.crosswalk}')
    print(f'  predios   : {a.predios}')
    print(f'  zona objetivo: {a.zona}')
    print()
    print('  El crosswalk es un DERIVADO de resolver_encuestas.py, que resuelve')
    print('  encuesta->predio por proximidad de superficie con TOLERANCIA_ABS=10.0 m2')
    print('  y EMPATE_MAX=0.5 m2. Estas constantes no tienen fundamento documentado.')
    print('  Todas las cifras siguientes heredan esa dependencia.')

    # ---------- encuestas ----------
    sep('1. ENCUESTAS: carga y limpieza')
    enc, notas_limpieza = {}, []
    with open(a.encuestas, encoding='utf-8-sig', newline='') as f:
        rd = csv.DictReader(f, delimiter=';')
        for r in rd:
            reg = int(r['Registro'])
            d = {}
            for campo, clave in (('Valor_Terreno_Bs', 'vt'), ('Sup_Terreno_m2', 'supdec'),
                                 ('Valor_Construccion_Bs', 'vc'), ('Valor_Total_Bs', 'vtot')):
                v, nota = num(r[campo])
                d[clave] = v
                if nota:
                    notas_limpieza.append((reg, campo, r[campo].strip(), nota))
            d['zona_csv'] = (r['Zona'] or '').strip()
            enc[reg] = d
    print(f'  filas leidas: {len(enc)}')
    print(f'  campos que requirieron limpieza: {len(notas_limpieza)}')
    for reg, campo, orig, nota in notas_limpieza:
        print(f'    reg {reg:>3}  {campo:<22} {orig!r:<16} -> {nota}')
    zc = {}
    for reg, d in enc.items():
        zc[d['zona_csv']] = zc.get(d['zona_csv'], 0) + 1
    print(f"  valores del campo Zona en el CSV: {zc}")

    # ---------- coherencia aritmetica ----------
    sep('2. COHERENCIA ARITMETICA terreno + construccion = total  (tol 1 Bs)')
    ok, bad, sin = 0, 0, 0
    detalle_bad = []
    for reg in sorted(enc):
        d = enc[reg]
        if d['vt'] is None or d['vtot'] is None:
            sin += 1
            continue
        vc = d['vc'] if d['vc'] is not None else 0.0
        dif = d['vt'] + vc - d['vtot']
        if abs(dif) <= 1.0:
            ok += 1
        else:
            bad += 1
            detalle_bad.append((reg, d['vt'], vc, d['vtot'], dif))
    print(f'  cuadran        : {ok}')
    print(f'  NO cuadran     : {bad}')
    print(f'  incalculables  : {sin}')
    if ok + bad:
        print(f'  tasa sobre calculables: {bad/(ok+bad)*100:.1f}%')
    print()
    print('  reg   terreno  construc     total   diferencia')
    for reg, vt, vc, vtot, dif in detalle_bad:
        print(f'  {reg:>3} {vt:>9.0f} {vc:>9.0f} {vtot:>9.0f} {dif:>12.0f}')

    # ---------- predios ----------
    sep('3. PREDIOS: carga de la salida SQL')
    pre, ignoradas = {}, 0
    for linea in open(a.predios, encoding='utf-8', errors='replace'):
        m = R_PREDIOS.match(linea)
        if not m:
            if linea.strip():
                ignoradas += 1
            continue
        uv, man, pred, sig, zona = m.groups()
        pre[(int(uv), int(man), int(pred))] = (float(sig), zona.strip())
    print(f'  tripletes cargados: {len(pre)}')
    print(f'  lineas ignoradas por no calzar el patron: {ignoradas}')
    dz = {}
    for _, z in pre.values():
        dz[z] = dz.get(z, 0) + 1
    print(f'  distribucion zonal del universo: {dict(sorted(dz.items()))}')

    # ---------- crosswalk y embudo ----------
    sep('4. EMBUDO DE ADMISION, FILA POR FILA')
    print('  reg  triplete    conf    zona   valor_terr   sup_sig      Bs/m2   estado')
    print('  ' + '-' * 76)
    admitidas, motivos = [], {}

    def desc(motivo):
        motivos[motivo] = motivos.get(motivo, 0) + 1

    total_cw = 0
    with open(a.crosswalk, encoding='utf-8-sig', newline='') as f:
        for r in csv.DictReader(f, delimiter=','):
            total_cw += 1
            reg = int(float(r['Registro']))
            cot = (r.get('COT_CAT') or '').strip()
            conf = (r.get('confianza') or '').strip()
            if not cot:
                desc('sin_resolucion_en_crosswalk')
                continue
            try:
                uv, man, pred = (int(x) for x in cot.split('-'))
            except ValueError:
                desc(f'COT_CAT_ilegible:{cot!r}')
                continue
            if (uv, man, pred) not in pre:
                desc('triplete_ausente_en_predios')
                print(f'  {reg:>3}  {cot:<10} {conf:<7} {"?":<6} '
                      f'{"":>10} {"":>9} {"":>10}   DESCARTADA triplete_ausente')
                continue
            sig, zona = pre[(uv, man, pred)]
            d = enc.get(reg)
            if d is None:
                desc('registro_ausente_en_encuestas')
                continue
            if zona != a.zona:
                desc(f'fuera_de_zona_{a.zona}(cae_en_{zona})')
                print(f'  {reg:>3}  {cot:<10} {conf:<7} {zona:<6} '
                      f'{(d["vt"] or 0):>10.0f} {sig:>9.2f} {"":>10}   DESCARTADA fuera_de_zona')
                continue
            if d['vt'] is None:
                desc('valor_terreno_ausente')
                print(f'  {reg:>3}  {cot:<10} {conf:<7} {zona:<6} '
                      f'{"":>10} {sig:>9.2f} {"":>10}   DESCARTADA sin_valor_terreno')
                continue
            if sig <= 0:
                desc('superficie_sig_no_positiva')
                continue
            b = d['vt'] / sig
            admitidas.append(dict(reg=reg, uv=uv, man=man, pred=pred, conf=conf,
                                  sig=sig, vt=d['vt'], vc=d['vc'], vtot=d['vtot'], b=b))
            print(f'  {reg:>3}  {cot:<10} {conf:<7} {zona:<6} '
                  f'{d["vt"]:>10.0f} {sig:>9.2f} {b:>10.2f}   ADMITIDA')

    print()
    print(f'  filas del crosswalk leidas : {total_cw}')
    print(f'  ADMITIDAS                  : {len(admitidas)}')
    print(f'  predios distintos admitidos: {len({(x["uv"],x["man"],x["pred"]) for x in admitidas})}')
    print('  descartes por motivo:')
    for k in sorted(motivos):
        print(f'    {k:<40} {motivos[k]}')

    if not admitidas:
        print('\n  Sin filas admitidas. Fin.')
        return 0

    # ---------- agregacion ----------
    sep('5. AGRUPACION POR MANZANA, clave (distrito, manzana)')
    g = {}
    for x in admitidas:
        g.setdefault((x['uv'], x['man']), []).append(x['b'])
    print(f'  manzanas con clave (distrito,manzana): {len(g)}')
    print(f'  manzanas si se agrupa solo por cod_man: {len({k[1] for k in g})}')
    print()
    print('  dist  man   n     promedio      mediana   valores')
    for k in sorted(g):
        v = sorted(g[k])
        print(f'  {k[0]:>4} {k[1]:>4} {len(v):>3} {st.mean(v):>12.2f} {st.median(v):>12.2f}   '
              + ' '.join(f'{y:.1f}' for y in v))

    sep('6. Vz POR METODO')
    proms = [st.mean(v) for v in g.values()]
    meds = [st.median(v) for v in g.values()]
    todos = sorted(x['b'] for x in admitidas)
    print(f'  n = {len(todos)} observaciones en {len(g)} manzanas')
    print()
    print(f'  A) Guia RM 024/2024, promedio de promedios por manzana : {st.mean(proms):>10.2f} Bs/m2')
    print(f'  B) ADR 0045 D6, mediana de medianas por manzana        : {st.median(meds):>10.2f} Bs/m2')
    print(f'  C) mediana simple de las observaciones                 : {st.median(todos):>10.2f} Bs/m2')
    print(f'  D) promedio simple de las observaciones                : {st.mean(todos):>10.2f} Bs/m2')
    print()
    print('  El metodo prescrito por la Guia es (A). (B) es el declarado en D6.')

    sep('7. DISPERSION')
    cv = st.stdev(todos) / st.mean(todos) * 100
    print(f'  minimo {todos[0]:.2f}   maximo {todos[-1]:.2f}   razon {todos[-1]/todos[0]:.0f}x')
    print(f'  desv.est. {st.stdev(todos):.2f}   coef. de variacion {cv:.0f}%')
    pv = sorted(proms)
    print(f'  promedios de manzana: min {pv[0]:.2f}  max {pv[-1]:.2f}  razon {pv[-1]/pv[0]:.0f}x')
    print(f'  coef. de variacion entre manzanas: {st.stdev(pv)/st.mean(pv)*100:.0f}%')

    sep('8. RUIDO DEL INSTRUMENTO: predios con mas de una encuesta')
    rep = {}
    for x in admitidas:
        rep.setdefault((x['uv'], x['man'], x['pred']), []).append((x['reg'], x['b']))
    hay = False
    for k in sorted(rep):
        v = rep[k]
        if len(v) < 2:
            continue
        hay = True
        vals = [b for _, b in v]
        print(f'  {k[0]}-{k[1]}-{k[2]}: ' + '   '.join(f'reg{r}={b:.2f}' for r, b in v)
              + f'   razon {max(vals)/min(vals):.2f}x')
    if not hay:
        print('  ninguno')
    else:
        razones = [max(b for _, b in v) / min(b for _, b in v)
                   for v in rep.values() if len(v) > 1]
        print(f'  razon mediana: {st.median(razones):.2f}x   razon maxima: {max(razones):.2f}x')

    sep('9. DESAGREGACION POR DISTRITO DENTRO DE LA ZONA')
    print('  ESTO ES DESCRIPTIVO. Excluir un distrito NO tiene fundamento normativo')
    print('  y no debe usarse para elegir un Vz.')
    print()
    print('  dist  manzanas  obs      Vz(A)   CV interno')
    for d in sorted({k[0] for k in g}):
        sub = {k: v for k, v in g.items() if k[0] == d}
        obs = [b for v in sub.values() for b in v]
        cvi = st.stdev(obs) / st.mean(obs) * 100 if len(obs) > 1 else float('nan')
        print(f'  {d:>4} {len(sub):>9} {len(obs):>4} {st.mean([st.mean(v) for v in sub.values()]):>10.2f}'
              f' {cvi:>11.0f}%')

    sep('10. VARIANTES (todas etiquetadas, ninguna es el resultado)')
    def variante(nombre, filas, nota=''):
        if not filas:
            print(f'  {nombre:<52} sin datos')
            return
        gg = {}
        for x in filas:
            gg.setdefault((x['uv'], x['man']), []).append(x['b'])
        pa = [st.mean(v) for v in gg.values()]
        ma = [st.median(v) for v in gg.values()]
        print(f'  {nombre:<52} A={st.mean(pa):>8.2f}  B={st.median(ma):>8.2f}'
              f'  (n={len(filas)}, mz={len(gg)}) {nota}')

    variante('base (todas las admitidas)', admitidas)
    variante('solo confianza ALTA', [x for x in admitidas if x['conf'] == 'ALTA'])
    variante('excluyendo distrito 1', [x for x in admitidas if x['uv'] != 1],
             'SIN FUNDAMENTO NORMATIVO')
    v2 = []
    for x in admitidas:
        if x['vtot'] is None:
            continue
        resto = x['vtot'] - (x['vc'] if x['vc'] is not None else 0.0)
        if resto <= 0:
            continue
        y = dict(x)
        y['b'] = resto / x['sig']
        v2.append(y)
    variante('terreno = total - construccion (Guia p.32)', v2)

    sep('11. MUESTRA CONTRA EL DISENO DEL CAP. IV DE LA GUIA')
    print('  El diseno exige 2 predios por el 50% de las manzanas de la zona.')
    print('  El total de manzanas de la zona NO es deducible de estos insumos;')
    print('  debe tomarse de una consulta aparte. Se reporta solo lo observado:')
    print(f'    manzanas efectivamente encuestadas y admitidas : {len(g)}')
    print(f'    observaciones admitidas                        : {len(admitidas)}')
    print('  La cobertura territorial y la calidad son deficiencias distintas y')
    print('  NO son separables con esta evidencia: menos manzanas implica mayor')
    print('  peso de cada promedio y por tanto mayor dispersion agregada.')

    sep('12. CONCENTRACION DE LOS VALORES DECLARADOS (las 198 encuestas)')
    vt_todos = [d['vt'] for d in enc.values() if d['vt'] is not None]
    cnt = collections.Counter(vt_todos)
    print(f'  observaciones con valor de terreno : {len(vt_todos)}')
    print(f'  valores distintos                  : {len(cnt)}')
    print()
    print('  valor declarado      veces    % del total')
    for v, n in cnt.most_common(12):
        print(f'  {v:>15,.0f} {n:>8} {n/len(vt_todos)*100:>13.1f}%')
    m10 = sum(n for v, n in cnt.items() if v % 10000 == 0)
    m5 = sum(n for v, n in cnt.items() if v % 5000 == 0)
    print()
    print(f'  multiplos exactos de 10.000 Bs : {m10}/{len(vt_todos)} = {m10/len(vt_todos)*100:.0f}%')
    print(f'  multiplos exactos de  5.000 Bs : {m5}/{len(vt_todos)} = {m5/len(vt_todos)*100:.0f}%')

    top_v, top_n = cnt.most_common(1)[0]
    grupo = [(reg, d['supdec'], top_v / d['supdec'])
             for reg, d in enc.items()
             if d['vt'] == top_v and d['supdec'] and d['supdec'] > 0]
    if len(grupo) > 1:
        b = [x[2] for x in grupo]
        print()
        print(f'  El valor {top_v:,.0f} Bs concentra {top_n/len(vt_todos)*100:.1f}% de las declaraciones.')
        print(f'  Sobre {len(grupo)} superficies distintas ({min(x[1] for x in grupo):.1f} a '
              f'{max(x[1] for x in grupo):.1f} m2) produce Bs/m2 de {min(b):.2f} a {max(b):.2f}')
        print(f'  = razon {max(b)/min(b):.1f}x, CV {st.stdev(b)/st.mean(b)*100:.0f}%, por pura aritmetica.')
        print()
        print('  IMPLICACION: la dispersion medida en la seccion 7 no es solo ruido de')
        print('  medicion. Una parte es un numerador convencional repetido dividido por')
        print('  denominadores variables. El metodo de autoavaluo de la Guia presupone')
        print('  declaraciones independientes por predio; esa premisa no se cumple.')

    sep('13. SENSIBILIDAD A LA ASIGNACION DESIGUAL DE LA MUESTRA')
    print('  El diseno del Cap. IV pide 2 predios por manzana. Distribucion real:')
    dist_n = collections.Counter(len(x) for x in g.values())
    for n in sorted(dist_n):
        marca = '  <- cumple el diseno' if n == 2 else ''
        print(f'    n={n}: {dist_n[n]:>3} manzanas{marca}')
    print()
    print('  El promedio de promedios pondera igual una media de 1 observacion y una')
    print('  de 7. Efecto de exigir un minimo de observaciones por manzana:')
    print()
    print('  umbral   manzanas   obs      Vz(A)      Vz(B)')
    for u in (1, 2, 3, 4):
        sub = {k: x for k, x in g.items() if len(x) >= u}
        if not sub:
            continue
        obs = sum(len(x) for x in sub.values())
        va = st.mean([st.mean(x) for x in sub.values()])
        vb = st.median([st.median(x) for x in sub.values()])
        print(f'   n>={u} {len(sub):>10} {obs:>5} {va:>10.2f} {vb:>10.2f}')
    ext_min = min(g.items(), key=lambda kv: st.mean(kv[1]))
    ext_max = max(g.items(), key=lambda kv: st.mean(kv[1]))
    print()
    print(f'  manzana con promedio MINIMO: {ext_min[0][0]}-{ext_min[0][1]}  '
          f'n={len(ext_min[1])}  {st.mean(ext_min[1]):.2f}')
    print(f'  manzana con promedio MAXIMO: {ext_max[0][0]}-{ext_max[0][1]}  '
          f'n={len(ext_max[1])}  {st.mean(ext_max[1]):.2f}')

    sep('14. SENSIBILIDAD A LA INCOHERENCIA ARITMETICA')
    incoh = set()
    for reg, d in enc.items():
        if d['vt'] is None or d['vtot'] is None:
            continue
        vc = d['vc'] if d['vc'] is not None else 0.0
        if abs(d['vt'] + vc - d['vtot']) > 1.0:
            incoh.add(reg)
    adm_incoh = [x for x in admitidas if x['reg'] in incoh]
    print(f'  encuestas con incoherencia aritmetica          : {len(incoh)}')
    print(f'  ADMITIDAS provenientes de filas incoherentes   : {len(adm_incoh)}'
          f'  = {len(adm_incoh)/len(admitidas)*100:.0f}% de las admitidas')
    print(f'  registros: {sorted(x["reg"] for x in adm_incoh)}')
    print()
    print('  Casos en que el total declarado es MENOR que el terreno declarado solo')
    print('  (una de las dos cifras es necesariamente falsa):')
    hay = False
    for x in sorted(admitidas, key=lambda y: y['reg']):
        d = enc[x['reg']]
        if d['vtot'] is None:
            continue
        if d['vtot'] < d['vt']:
            hay = True
            vc = d['vc'] if d['vc'] is not None else 0.0
            print(f'    reg {x["reg"]:>3}: terreno {d["vt"]:>10,.0f} + constr {vc:>10,.0f}'
                  f' = {d["vt"]+vc:>10,.0f}   total declarado {d["vtot"]:>10,.0f}'
                  f'   -> {x["b"]:.2f} Bs/m2')
    if not hay:
        print('    ninguno entre las admitidas')
    limpias = [x for x in admitidas if x['reg'] not in incoh]
    if limpias:
        gl = {}
        for x in limpias:
            gl.setdefault((x['uv'], x['man']), []).append(x['b'])
        print()
        print(f'  Excluyendo las filas incoherentes: n={len(limpias)}, mz={len(gl)}')
        print(f'    Vz(A) = {st.mean([st.mean(x) for x in gl.values()]):.2f} Bs/m2')
        print(f'    Vz(B) = {st.median([st.median(x) for x in gl.values()]):.2f} Bs/m2')

    sep('15. TASA DE ERROR MEDIDA DEL CROSSWALK')
    fuera = sum(n for k, n in motivos.items() if k.startswith('fuera_de_zona'))
    resueltas = fuera + len(admitidas) + sum(
        n for k, n in motivos.items()
        if not k.startswith('fuera_de_zona') and k != 'sin_resolucion_en_crosswalk')
    if resueltas:
        print(f'  resoluciones del crosswalk evaluadas : {resueltas}')
        print(f'  contradicen la zona declarada        : {fuera}'
              f'  = {fuera/resueltas*100:.1f}%')
    print('  Esta es una cota INFERIOR del error de TOLERANCIA_ABS=10.0 y')
    print('  EMPATE_MAX=0.5: los falsos positivos que caen dentro de la propia')
    print(f'  zona {a.zona} son indetectables por este control.')
    return 0




if __name__ == '__main__':
    sys.exit(main())
