import dnfile

DLL = '/root/.nuget/packages/riskofrain2.gamelibs/1.5.0-r.1/lib/netstandard2.0/RoR2.dll'
pe = dnfile.dnPE(DLL)
md = pe.net.mdtables

def rs(row, attr):
    v = getattr(row, attr, None)
    if v is None: return ''
    try: return str(v.value)
    except: return str(v)

td_rows = md.TypeDef.rows
found = 0
for i, td in enumerate(td_rows):
    n = rs(td, 'Name')
    ns = rs(td, 'TypeNamespace')
    if n in ('Inventory','PickupFinder','ItemDrop'):
        found += 1
        print(f"TypeDef[{i}] {ns}.{n} methodlist={td.MethodList}")
print("total found:", found, "of", len(td_rows))
