import dnfile
pe = dnfile.dnPE('/root/.nuget/packages/riskofrain2.gamelibs/1.5.0-r.1/lib/netstandard2.0/RoR2.dll')
td_rows = pe.net.mdtables.TypeDef.rows
md_rows = pe.net.mdtables.MethodDef.rows

# find TypeDefs whose name is Inventory or PickupFinder etc
targets = ['Inventory', 'PickupFinder', 'ItemDrop', 'ScenePlacedDirector']
for i, td in enumerate(td_rows):
    if str(td.Name) in targets:
        start = td.MethodList.row_index - 1
        end = td_rows_end = (td_rows[i+1].MethodList.row_index - 1) if i+1 < len(td_rows) else len(md_rows)
        print(f"=== {td.TypeNamespace}.{td.Name} ===")
        for m in md_rows[start:end]:
            n = str(m.Name)
            if any(k in n for k in ['Give','Add','Pickup','Assign','Roll']):
                print(" ", n)
