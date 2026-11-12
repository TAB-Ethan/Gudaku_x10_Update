#!/usr/bin/env python3
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
import dnfile

pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
md = pe.net.mdtables

def bb(x):
    v = x.value
    if callable(v): v = v()
    return bytes(v)

def coded(o):
    if o is None: return (None, None)
    t = o.table
    tn = getattr(t, '__name__', None) or str(t)
    r = o.row
    ri = getattr(r, 'row_index', None)
    rr = ri + 1 if ri is not None else r
    return (tn, rr)

# TypeRef names with scope
def trname(i):
    r = md.TypeRef.rows[i-1]
    st, sr = coded(r.ResolutionScope)
    sn = ''
    if st == 'AssemblyRef': sn = md.AssemblyRef.rows[sr-1].Name
    elif st == 'TypeRef': sn = f"TR{sr}"
    return f"{sn}|{r.TypeNamespace}.{r.TypeName}"

TRS = [trname(i+1) for i in range(len(md.TypeRef.rows))]

# MemberRef resolution
def mrname(i):
    r = md.MemberRef.rows[i-1]
    ct, cr = coded(r.Class)
    if ct == 'TypeRef':
        cn = TRS[cr-1]
    elif ct == 'TypeSpec':
        cn = f"TS{cr}[{bb(md.TypeSpec.rows[cr-1].Signature).hex()}]"
    elif ct == 'ModuleRef':
        cn = f"ModRef:{md.ModuleRef.rows[cr-1].Name}"
    else:
        cn = f"{ct}{cr}"
    return f"{cn}::{r.Name} sig={bb(r.Signature).hex()}"

MRS = [mrname(i+1) for i in range(len(md.MemberRef.rows))]

print("=== MemberRef resolved ===")
for i,m in enumerate(MRS): print(f"MR{i+1}: {m}")

print("\n=== TypeSpec ===")
for i,r in enumerate(md.TypeSpec.rows): print(f"TS{i+1}: {bb(r.Signature).hex()}")

print("\n=== MethodSpec ===")
for i,r in enumerate(md.MethodSpec.rows):
    mt, mr_ = coded(r.Method)
    desc = MRS[mr_-1] if mt=='MemberRef' else f"{mt}{mr_}"
    print(f"MS{i+1}: {desc} inst={bb(r.Instantiation).hex()}")

print("\n=== Fields ===")
for i,r in enumerate(md.Field.rows):
    print(f"F{i+1}: {r.Name} sig={bb(r.Signature).hex()}")

print("\n=== CustomAttribute ===")
for i,r in enumerate(md.CustomAttribute.rows):
    pt, pr = coded(r.Parent)
    ct, cr = coded(r.Type)
    try:
        if ct == 'MethodDef':
            td_row = md.MethodDef.rows[cr-1]
            # find parent type via TypeDef row list
            attname = '?'
            for ti,trow in enumerate(md.Typedef.rows):
                if trow.MethodList and trow.MethodList.row_index is not None:
                    pass
        att = None
        if ct == 'MemberRef': att = MRS[cr-1]
        elif ct == 'MethodDef': att = f"MethodDef{cr}:{md.MethodDef.rows[cr-1].Name}"
        pn = f"{pt}{pr}"
        if pt=='TypeDef': pn=f"type {md.Typedef.rows[pr-1].TypeNamespace}.{md.Typedef.rows[pr-1].TypeName}"
        if pt=='MethodDef': pn=f"method {md.MethodDef.rows[pr-1].Name}"
        if pt=='Field': pn=f"field {md.Field.rows[pr-1].Name}"
        if pt=='Assembly': pn='assembly'
        if pt=='Module': pn='module'
        val = bb(r.Value)[:60].hex()
        print(f"CA{i+1}: on {pn} attr={att} val={val}")
    except Exception as e:
        print(f"CA{i+1}: ERR {e}")
