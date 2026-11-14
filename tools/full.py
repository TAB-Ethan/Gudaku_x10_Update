import dnfile, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
md = pe.net.mdtables
us = pe.net.user_strings  # might exist
try:
    for off in us.keys():
        print(f"US@{off}: {us[off]!r}")
except Exception as e:
    print("us err", e)

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

print("\n=== TypeDef ===")
for i,r in enumerate(md.TypeDef.rows):
    print(f"TD{i+1}: {r.TypeNamespace}.{r.TypeName} extends={coded(r.Extends) if r.Extends else None} fields={coded(r.FieldList)} methods={coded(r.MethodList)} flags={str(r.Flags)[:50]}")

print("\n=== AssemblyRef ===")
for i,r in enumerate(md.AssemblyRef.rows):
    print(f"AR{i+1}: {r.Name} v{r.MajorVersion}.{r.MinorVersion}.{r.BuildNumber}.{r.RevisionNumber}")

print("\n=== MemberRef ===")
def trname(i):
    r = md.TypeRef.rows[i-1]
    st, sr = coded(r.ResolutionScope)
    sn = ''
    if st == 'AssemblyRef': sn = md.AssemblyRef.rows[sr-1].Name
    elif st == 'TypeRef': sn = f"TR{sr}"
    return f"{sn}|{r.TypeNamespace}.{r.TypeName}"
TRS=[trname(i+1) for i in range(len(md.TypeRef.rows))]
MRS=[]
for i,r in enumerate(md.MemberRef.rows):
    ct, cr = coded(r.Class)
    if ct=='TypeRef': cn=TRS[cr-1]
    elif ct=='TypeSpec': cn=f"TS{cr}[{bb(md.TypeSpec.rows[cr-1].Signature).hex()}]"
    elif ct=='ModuleRef': cn=f"ModRef:{md.ModuleRef.rows[cr-1].Name}"
    else: cn=f"{ct}{cr}"
    MRS.append(f"{cn}::{r.Name} sig={bb(r.Signature).hex()}")
    print(f"MR{i+1}: {MRS[-1]}")

print("\n=== TypeSpec ===")
for i,r in enumerate(md.TypeSpec.rows): print(f"TS{i+1}: {bb(r.Signature).hex()}")

print("\n=== MethodSpec ===")
for i,r in enumerate(md.MethodSpec.rows):
    mt, mr_ = coded(r.Method)
    print(f"MS{i+1}: {mt}{mr_} inst={bb(r.Instantiation).hex()}")

print("\n=== Field ===")
for i,r in enumerate(md.Field.rows): print(f"F{i+1}: {r.Name} sig={bb(r.Signature).hex()} flags={str(r.Flags)[:30]}")

print("\n=== CustomAttribute ===")
for i,r in enumerate(md.CustomAttribute.rows):
    pt, pr = coded(r.Parent); ct, cr = coded(r.Type)
    print(f"CA{i+1}: parent=({pt},{pr}) type=({ct},{cr}) val={bb(r.Value)[:90].hex()}")

print("\n=== Param ===")
for i,r in enumerate(md.Param.rows): print(f"P{i+1}: seq={r.Sequence} name={r.Name}")

print("\n=== MethodDef ===")
for i,r in enumerate(md.MethodDef.rows): print(f"M{i+1}: {r.Name} RVA=0x{r.Rva:X} sig={bb(r.Signature).hex()} impl={str(r.ImplFlags)[:20]} flags={str(r.Flags)[:40]}")
