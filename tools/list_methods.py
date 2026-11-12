import dnfile

DLL = '/root/.nuget/packages/riskofrain2.gamelibs/1.5.0-r.1/lib/netstandard2.0/RoR2.dll'
pe = dnfile.dnPE(DLL)
md = pe.net.mdtables

def row_str(row, attr):
    v = getattr(row, attr, None)
    if v is None: return ''
    try: return str(v.value)
    except: return str(v)

td_rows = md.TypeDef.rows
md_rows = md.MethodDef.rows

targets = ['Inventory', 'PickupFinder', 'ItemDrop']
for i, td in enumerate(td_rows):
    name = row_str(td, 'Name')
    if name in targets and row_str(td, 'TypeNamespace') == 'RoR2':
        start = td.MethodList.value - 1
        end = td_rows[i+1].MethodList.value - 1 if i+1 < len(td_rows) else len(md_rows)
        print(f"=== RoR2.{name} ===")
        for m in md_rows[start:end]:
            mn = row_str(m, 'Name')
            if any(k in mn for k in ['Give','Add','Pickup','Assign']):
                print(" ", mn)
