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
    """Return (table_name_str, 1-based row int)."""
    if o is None: return (None, None)
    t = o.table                      # table class object or str
    tn = getattr(t, '__name__', None) or str(t)
    r = o.row                        # row OBJECT in this dnfile version
    ri = getattr(r, 'row_index', None)
    rr = ri + 1 if ri is not None else r
    return (tn, rr)

def trname(i):
    r = md.TypeRef.rows[i-1]
    st, sr = coded(r.ResolutionScope)
    sn = ''
    if st == 'AssemblyRef': sn = md.AssemblyRef.rows[sr-1].Name
    elif st == 'TypeRef': sn = f"TR{sr}"
    elif st == 'Module': sn = '<this>'
    return f"[{sn}] {r.TypeNamespace}.{r.TypeName}"

print("=== TypeRef ===")
for i, r in enumerate(md.TypeRef.rows):
    print(f"TR{i+1}: {trname(i+1)}")

print("\n=== MethodDef ===")
for i, r in enumerate(md.MethodDef.rows):
    print(f"M{i+1}: {r.Name} RVA=0x{r.Rva:X} sig={bb(r.Signature).hex()} flags={'static' if 'True' in str(r.Flags.mdStatic) else ''}")

print("\n=== Params (all) ===")
for i,r in enumerate(md.Param.rows):
    print(f"P{i+1}: seq={r.Sequence} name={r.Name}")

print("\n=== MemberRef ===")
for i, r in enumerate(md.MemberRef.rows):
    ct, cr = coded(r.Class)
    if ct == 'TypeRef': cn = trname(cr)
    elif ct == 'TypeSpec': cn = f"TS{cr}[{bb(md.TypeSpec.rows[cr-1].Signature).hex()}]"
    elif ct == 'ModuleRef': cn = f"ModRef:{md.ModuleRef.rows[cr-1].Name}"
    else: cn = f"{ct}{cr}"
    print(f"MR{i+1} (tok 0x{0x0A000000+i+1:08X}): {cn} :: {r.Name} sig={bb(r.Signature).hex()}")

print("\n=== TypeSpec ===")
for i, r in enumerate(md.TypeSpec.rows):
    print(f"TS{i+1} (tok 0x{0x1B000000+i+1:08X}): {bb(r.Signature).hex()}")

print("\n=== MethodSpec ===")
for i, r in enumerate(md.MethodSpec.rows):
    mt, mr_ = coded(r.Method)
    desc = '?'
    if mt == 'MemberRef':
        x = md.MemberRef.rows[mr_-1]
        ct, cr = coded(x.Class)
        if ct == 'TypeRef': cn = trname(cr)
        elif ct == 'TypeSpec': cn = f"TS{cr}[{bb(md.TypeSpec.rows[cr-1].Signature).hex()}]"
        else: cn = f"{ct}{cr}"
        desc = f"{cn}::{x.Name} membersig={bb(x.Signature).hex()}"
    print(f"MS{i+1} (tok 0x{0x2B000000+i+1:08X}): {desc} inst={bb(r.Instantiation).hex()}")

print("\n=== CustomAttribute ===")
for i, r in enumerate(md.CustomAttribute.rows):
    pt, pr = coded(r.Parent)
    if pt == 'TypeDef':
        td = md.Typedef.rows[pr-1]; pn = f"type {td.TypeNamespace}.{td.TypeName}"
    elif pt == 'MethodDef': pn = f"method {md.MethodDef.rows[pr-1].Name}"
    elif pt == 'Field': pn = f"field {md.Field.rows[pr-1].Name}"
    elif pt == 'Assembly': pn = 'assembly'
    elif pt == 'Module': pn = 'module'
    else: pn = f"{pt}{pr}"
    tt, tr_ = coded(r.Type)
    an = '?'; cs = b''
    if tt == 'MemberRef':
        x = md.MemberRef.rows[tr_-1]
        cs = bb(x.Signature)
        ct, cr = coded(x.Class)
        if ct == 'TypeRef': an = trname(cr) + f"::{x.Name}"
        else: an = f"{ct}{cr}::{x.Name}"
    elif tt == 'MethodDef':
        an = f"MD:{md.MethodDef.rows[tr_-1].Name}"
        cs = bb(md.MethodDef.rows[tr_-1].Signature)
    val = bb(r.Value)
    print(f"CA{i+1}: {pn} <- {an} ctor_sig={cs.hex()} val={val.hex()}")

print("\n=== FieldRva ===")
tbl = getattr(md, 'FieldRva', None) or getattr(md, 'FieldRVA', None)
if tbl:
    for i, r in enumerate(tbl.rows):
        ft, fr = coded(r.Field)
        fld = md.Field.rows[fr-1]
        data = pe.get_data(f"0x{r.Rva:X}", 64) or b''
        print(f"FRVA{i+1}: {fld.Name} rva=0x{r.Rva:X} data={data[:32].hex()}")

print("\n=== Typedef ===")
for i, r in enumerate(md.Typedef.rows):
    et, er = coded(r.Extends)
    ex = trname(er) if et == 'TypeRef' else f"{et}{er}"
    print(f"TD{i+1}: {r.TypeNamespace}.{r.TypeName} extends {ex} M@{r.MethodList[0].row_index+1 if r.MethodList else '?'} F@{r.FieldList[0].row_index+1 if r.FieldList else '?'}")

print("\n=== NestedClass ===")
nc = getattr(md, 'NestedClass', None)
if nc:
    for i, r in enumerate(nc.rows):
        nt, nr = coded(r.NestedClass)
        en, enrow = coded(r.EnclosingClass)
        print(f"N{i+1}: {md.Typedef.rows[nr-1].TypeName} nested in {md.Typedef.rows[enrow-1].TypeName}")
