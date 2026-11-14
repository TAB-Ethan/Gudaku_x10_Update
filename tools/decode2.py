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
    t=getattr(o,'table',None)
    tn=None
    if t is not None:
        tn=getattr(t,'__name__',None) or str(type(t).__name__)
    r=o.row
    rr=getattr(r,'row_index',None)
    if rr is not None: r=rr+1
    return (tn,r)

# ---- TypeRef ----
print("=== TypeRef ===")
for i, r in enumerate(md.TypeRef.rows):
    st, sr = coded(r.ResolutionScope)
    sn = ''
    if st == 'AssemblyRef': sn = md.AssemblyRef.rows[sr-1].Name
    elif st == 'Module': sn = '<this>'
    elif st == 'TypeRef': sn = f"nested-of-TR{sr}"
    print(f"TR{i+1}: [{sn}] {r.TypeNamespace}.{r.TypeName}")

# ---- MethodDef ----
print("\n=== MethodDef ===")
for i, r in enumerate(md.MethodDef.rows):
    print(f"M{i+1}: {r.Name} RVA=0x{r.Rva:X} sig={bb(r.Signature).hex()} attrs={int(r.ImplFlags if False else getattr(r,"Attributes",0)):X}")

# ---- Param ----
print("\n=== Param ===")
for i, r in enumerate(md.Param.rows):
    print(f"P{i+1}: {r.Name} seq={r.Sequence}")

# ---- MemberRef ----
print("\n=== MemberRef ===")
for i, r in enumerate(md.MemberRef.rows):
    ct, cr = coded(r.Class)
    if ct == 'TypeRef':
        tr = md.TypeRef.rows[cr-1]; cn = f"{tr.TypeNamespace}.{tr.TypeName}"
    elif ct == 'MethodDef':
        cn = f"MD:{md.MethodDef.rows[cr-1].Name}"
    elif ct == 'TypeSpec':
        cn = f"TS{cr}(sig {bb(md.TypeSpec.rows[cr-1].Signature).hex()})"
    elif ct == 'ModuleRef':
        cn = f"ModRef:{md.ModuleRef.rows[cr-1].Name}"
    else:
        cn = str(ct)
    print(f"MR{i+1} (tok 0x{0x0A000000+i+1:08X}): {cn}::{r.Name} sig={bb(r.Signature).hex()}")

# ---- TypeSpec ----
print("\n=== TypeSpec ===")
for i, r in enumerate(md.TypeSpec.rows):
    print(f"TS{i+1} (tok 0x{0x1B000000+i+1:08X}): {bb(r.Signature).hex()}")

# ---- MethodSpec ----
print("\n=== MethodSpec ===")
if md.MethodSpec:
    for i, r in enumerate(md.MethodSpec.rows):
        t, row = coded(r.Method)
        name = '?'
        if t == 'MethodDef':
            name = f"MD:{md.MethodDef.rows[row-1].Name}"
        elif t == 'MemberRef':
            mr = md.MemberRef.rows[row-1]
            ct, cr = coded(mr.Class)
            if ct == 'TypeSpec':
                tsb = bb(md.TypeSpec.rows[cr-1].Signature).hex()
                name = f"MR-on-TS{cr}[{tsb}]::{mr.Name}"
            elif ct == 'TypeRef':
                tr = md.TypeRef.rows[cr-1]
                name = f"{tr.TypeNamespace}.{tr.TypeName}::{mr.Name}"
            else:
                name = f"{ct}{cr}::{mr.Name}"
        print(f"MS{i+1} (tok 0x{0x2B000000+i+1:08X}): {name} instsig={bb(r.Instantiation).hex()}")

# ---- CustomAttribute ----
print("\n=== CustomAttribute ===")
def typeref_or_tokensig(tok_int):
    tt = (tok_int >> 24) & 0xFF
    rid = tok_int & 0xFFFFFF
    if tt == 0x01:
        r = md.TypeRef.rows[rid-1]; return f"[{r.TypeNamespace}]{r.TypeName}"
    if tt == 0x02:
        r = md.Typedef.rows[rid-1]; return f"{r.TypeNamespace}.{r.TypeName}"
    if tt == 0x1B:
        return f"TS{rid}[{bb(md.TypeSpec.rows[rid-1].Signature).hex()}]"
    return f"tok:{tok_int:08X}"

def decode_blob_vals(b):
    """Decode SerProbs of a CA blob after prolog. Returns list of (kind,val)."""
    # b starts with 01 00 ...
    out = []
    i = 2
    def com_i():
        nonlocal i
        x = b[i]; i += 1
        if x & 0x80:
            y = b[i]; i += 1
            return ((x & 0x7F) << 6) | (y & 0x3F)
        return x
    n = com_i()
    out.append(('count', n))
    for _ in range(n):
        tag = b[i]; i += 1
        if tag == 0x15:
            elem = b[i]; i += 1
            if elem == 0x1D:
                td = b[i] | (b[i+1]<<8) | (b[i+2]<<16) | (b[i+3]<<24); i += 4
                which = td & 1; tok = td >> 1
                inner = typeref_or_tokensig((0x02000000|tok) if which==0 else (0x01000000|tok))
                argc = com_i()
                args = []
                for _a in range(argc):
                    atag = b[i]; i += 1
                    ae = b[i]; i += 1
                    if ae == 0x0E:
                        ln = com_i()
                        s = b[i:i+ln].decode('utf-8'); i += ln
                        args.append(f'String:{s!r}')
                    else:
                        args.append(f'et{ae:02X}@{i}')
                out.append(('box', f'{inner}<{"|".join(args)}>'))
            else:
                out.append(('elem', f'et{elem:02X}'))
        else:
            out.append(('tag', f'0x{tag:02X}'))
    return out

for i, r in enumerate(md.CustomAttribute.rows):
    pt, pr = coded(r.Parent)
    if pt == 'TypeDef':
        td = md.Typedef.rows[pr-1]; pn = f"type {td.TypeNamespace}.{td.TypeName}"
    elif pt == 'MethodDef':
        pn = f"method {md.MethodDef.rows[pr-1].Name}"
    elif pt == 'Field':
        pn = f"field {md.Field.rows[pr-1].Name}"
    elif pt == 'Assembly':
        pn = 'assembly'
    elif pt == 'Parameter':
        pn = f"param{pr}"
    else:
        pn = f"{pt}{pr}"
    at, ar = coded(r.Type)
    an = '?'
    ctor_sig = None
    if at == 'MemberRef':
        mr = md.MemberRef.rows[ar-1]
        ctor_sig = bb(mr.Signature)
        ct, cr = coded(mr.Class)
        if ct == 'TypeRef':
            tr = md.TypeRef.rows[cr-1]; an = f"{tr.TypeNamespace}.{tr.TypeName}::{mr.Name}"
        else:
            an = f"{ct}{cr}::{mr.Name}"
    elif at == 'MethodDef':
        an = f"MD:{md.MethodDef.rows[ar-1].Name}"
        ctor_sig = bb(md.MethodDef.rows[ar-1].Signature)
    val = bb(r.Value)
    extra = decode_blob_vals(val) if len(val) >= 2 and val[0] == 1 else []
    print(f"CA{i+1}: {pn} <- {an} val={val.hex()} dec={extra}")
    if ctor_sig:
        print(f"      ctor sig: {ctor_sig.hex()}")

# ---- FieldRVA ----
print("\n=== FieldRVA ===")
if md.FieldRva:
    for i, r in enumerate(md.FieldRva.rows):
        ft, fr = coded(r.Field)
        f = md.Field.rows[fr-1]
        data = pe.get_data(f"0x{r.Rva:X}", 64)
        print(f"FRVA{i+1}: field {f.Name} rva=0x{r.Rva:X} data={data.hex()[:64]}")

# ---- Typedef layout ----
print("\n=== Typedef ===")
for i, r in enumerate(md.Typedef.rows):
    et, er = coded(r.Extends)
    ex = ''
    if et == 'TypeDef': ex = f"TD{er}:{md.Typedef.rows[er-1].TypeName}"
    elif et == 'TypeRef':
        tr = md.TypeRef.rows[er-1]; ex = f"TR{er}:{tr.TypeNamespace}.{tr.TypeName}"
    print(f"TD{i+1}: {r.TypeNamespace}.{r.TypeName} extends {ex} methods@{r.MethodList.row if r.MethodList else '?'} fields@{r.FieldList.row if r.FieldList else '?'}")

# ---- InterfaceImpl / nested ----
print("\n=== NestedClass ===")
if md.NestedClass:
    for i, r in enumerate(md.NestedClass.rows):
        nt, nr = coded(r.NestedClass); en, enrow = coded(r.EnclosingClass)
        nn = md.Typedef.rows[nr-1].TypeName
        try:
            ename = md.Typedef.rows[enrow-1].TypeName
        except Exception:
            ename = '?'
        print(f"N{i+1}: {nn} nested in {ename}")
