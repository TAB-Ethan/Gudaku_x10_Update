import dnfile

DLL = '/root/.nuget/packages/riskofrain2.gamelibs/1.5.0-r.1/lib/netstandard2.0/RoR2.dll'
pe = dnfile.dnPE(DLL)
md = pe.net.mdtables
td_rows = md.TypeDef.rows
md_rows = md.MethodDef.rows

for i, td in enumerate(td_rows):
    if td.Name == 'Inventory' and td.TypeNamespace == 'RoR2':
        start = td.MethodList.value - 1
        end = td_rows[i+1].MethodList.value - 1 if i+1 < len(td_rows) else len(md_rows)
        print(f"=== RoR2.Inventory (methods {start}..{end}) ===")
        for m in md_rows[start:end]:
            print(" ", m.Name)
        break
