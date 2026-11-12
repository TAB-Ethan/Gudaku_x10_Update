import dnfile, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
md = pe.net.mdtables
def bb(x):
    v = x.value
    if callable(v): v = v()
    return bytes(v)
def coded(o):
    t = o.table
    tn = getattr(t,'__name__',None) or str(t)
    r = o.row
    ri = getattr(r,'row_index',None)
    return (tn, ri+1 if ri is not None else r)

# UserString heap
heap = None
for sec in [s for s in pe.net.metadata.streams_list if hasattr(s,'name')]:
    if sec.name == b'#US': heap = sec.get_data()
print("=== #US heap strings ===")
i = 0
while i < len(heap):
    try:
        # compressed uint length
        b0 = heap[i]
        if b0 & 0x80: break
        slen = b0
        if slen == 0: i+=1; continue
        s = heap[i+1:i+1+slen].decode('utf-16-le', errors='replace')
        extra = heap[i+1+slen]
        print(f"US@{i}: {s!r} term={extra}")
        i += 1 + slen + 1
    except Exception as e:
        print("ERR at", i, e); break

print("\n=== TypeDef rows ===")
for i,r in enumerate(md.Typedef.rows):
    fl = str(r.Flags)
    print(f"TD{i+1}: {r.TypeNamespace}.{r.TypeName} extends={coded(r.Extends) if r.Extends else None} fields={coded(r.FieldList)} methods={coded(r.MethodList)} flags={fl[:40]}")

print("\n=== MemberRef class resolution fixed ===")
def trname(i):
    r = md.TypeRef.rows[i-1]
    st, sr = coded(r.ResolutionScope)
    sn = ''
    if st == 'AssemblyRef': sn = md.AssemblyRef.rows[sr-1].Name
    elif st == 'TypeRef': sn = f"TR{sr}"
    return f"{sn}|{r.TypeNamespace}.{r.TypeName}"
TRS=[trname(i+1) for i in range(len(md.TypeRef.rows))]
for i,r in enumerate(md.MemberRef.rows):
    ct, cr = coded(r.Class)
    if ct=='TypeRef': cn=TRS[cr-1]
    elif ct=='TypeSpec': cn=f"TS{cr}[{bb(md.TypeSpec.rows[cr-1].Signature).hex()}]"
    elif ct=='ModuleRef': cn=f"ModRef:{md.ModuleRef.rows[cr-1].Name}"
    else: cn=f"{ct}{cr}"
    print(f"MR{i+1}: {cn}::{r.Name} sig={bb(r.Signature).hex()}")

print("\n=== MethodSpec ===")
for i,r in enumerate(md.MethodSpec.rows):
    mt, mr_ = coded(r.Method)
    print(f"MS{i+1}: {mt}{mr_} inst={bb(r.Instantiation).hex()}")

print("\n=== StandAloneSig ===")
for i,r in enumerate(md.StandAloneSig.rows):
    print(f"SAS{i+1}: {bb(r.Signature).hex()}")

print("\n=== FieldPtr/Field rows ===")
for i,r in enumerate(md.Field.rows):
    print(f"F{i+1}: {r.Name} sig={bb(r.Signature).hex()} flags={str(r.Flags)[:30]}")

print("\n=== CustomAttribute ===")
for i,r in enumerate(md.CustomAttribute.rows):
    pt, pr = coded(r.Parent)
    ct, cr = coded(r.Type)
    val = bb(r.Value)
    print(f"CA{i+1}: parent=({pt},{pr}) type=({ct},{cr}) val={val[:80].hex()}")
