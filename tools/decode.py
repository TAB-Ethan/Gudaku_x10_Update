#!/usr/bin/env python3
"""Full-metadata decoder for Gudako-x10.dll using dnfile."""
import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
import dnfile
from dnfile.enums import MetadataTables


def blob_bytes(x):
    if isinstance(x,(bytes,bytearray)): return bytes(x)
    v=x.value
    if callable(v): v=v()
    if isinstance(v,str): v=v.encode('latin1')
    return bytes(v)

pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
md = pe.net.mdtables

def tname(tok):
    if tok is None: return 'NULL'
    t = tok.table
    try:
        row = md.__getattr__(t).rows[tok.row - 1]
        return f"{t}:{row.Name}"
    except Exception:
        return f"{t}:{tok.row}(?)"

def type_name(idx):
    """Decode a TypeDefOrRef coded index or table token to readable name."""
    if idx is None: return 'NULL'
    if isinstance(idx, int):
        tt = (idx >> 24) & 0xFF
        rid = idx & 0xFFFFFF
        if tt == 0x02:
            r = md.Typedef.rows[rid-1]
            ns = r.TypeNamespace or ''
            return f"{ns}.{r.TypeName}" if ns else r.TypeName
        if tt == 0x01:
            r = md.TypeRef.rows[rid-1]
            ns = r.TypeNamespace or ''
            scope = ''
            try:
                scope = tname(r.TypeResolutionScope)
            except Exception: pass
            return f"{scope}/{ns}.{r.TypeName}" if (ns or scope) else r.TypeName
        return f"tok:{idx:08X}"
    # coded index object
    try:
        tagval = idx.table if hasattr(idx,'table') else None
        row = idx.row if hasattr(idx,'row') else None
        if tagval and row:
            r = md.__getattr__(tagval).rows[row-1]
            nm = getattr(r,'TypeName',None) or getattr(r,'Name','?')
            ns = getattr(r,'TypeNamespace',None)
            return f"{ns}.{nm}" if ns else str(nm)
    except Exception as e:
        return f"codedidx({idx.Value if hasattr(idx,'Value') else idx})"
    return str(idx)

def method_name(mtok):
    if mtok is None: return 'NULL'
    if isinstance(mtok, int):
        tt=(mtok>>24)&0xFF; rid=mtok&0xFFFFFF
        if tt==0x06:
            r=md.Method.rows[rid-1]
            cls=''
            try:
                # find parent via reverse lookup not trivial; skip
                pass
            except Exception: pass
            return f"{r.Name}"
        if tt==0x0A:
            r=md.MemberRef.rows[rid-1]
            par = tname(r.Class)
            return f"{par}::{r.Name}"
        return f"tok:{mtok:08X}"
    return str(mtok)

def field_name(ftok):
    if ftok is None: return 'NULL'
    if isinstance(ftok,int):
        tt=(ftok>>24)&0xFF; rid=ftok&0xFFFFFF
        if tt==0x04:
            r=md.Field.rows[rid-1]; return r.Name
        if tt==0x0A:
            r=md.MemberRef.rows[rid-1]
            return f"{tname(r.Class)}::{r.Name}"
        return f"tok:{ftok:08X}"
    return str(ftok)

print("=== Assembly ===")
for r in md.Assembly.rows:
    print(f"Name={r.Name} Ver={r.MajorVersion}.{r.MinorVersion}.{r.BuildNumber}.{r.RevisionNumber}")
print("=== AssemblyRef ===")
for i,r in enumerate(md.AssemblyRef.rows):
    print(f"[{i+1}] {r.Name} v{r.MajorVersion}.{r.MinorVersion}.{r.BuildNumber}.{r.RevisionNumber} ")
print("=== ModuleRef ===")
for i,r in enumerate(md.ModuleRef.rows if md.ModuleRef else []):
    print(f"[{i+1}] {r.Name}")

print("\n=== Fields (with decoded sig) ===")
def parse_field_sig(blob):
    # blob: [0x06 flags] Type ...
    # simplified: extract element type + optional class name token
    out=[]
    i=0
    b=blob_bytes(blob)
    flags=b[i]; i+=1
    # parse one type
    def read_type(i):
        et=b[i]; i+=1
        if et==0x0E: return ('string',i)
        if et==0x08: return ('object',i)
        if et==0x0C: return ('int',i)
        if et==0x11: # valuetype
            td=(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24))>>1
            which=td&1; tok=td>>1
            i+=4
            tn = type_name((0x02000000|tok) if which==0 else (0x01000000|tok))
            return (f"value:{tn}",i)
        if et==0x12: # class
            td=(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24))>>1
            which=td&1; tok=td>>1
            i+=4
            tn = type_name((0x02000000|tok) if which==0 else (0x01000000|tok))
            return (f"class:{tn}",i)
        if et==0x1D: # generic instance
            td=(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24))>>1
            which=td&1; tok=td>>1
            i+=4
            tn = type_name((0x02000000|tok) if which==0 else (0x01000000|tok))
            argc=b[i]; i+=1
            args=[]
            for _ in range(argc):
                a,i=read_type(i); args.append(a)
            return (f"{tn}<{','.join(args)}>",i)
        if et==0x0F: # ptr
            a,i=read_type(i); return (a+'*',i)
        return (f"et:{et:02X}",i)
    t,i=read_type(i)
    static = bool(flags & 0x10)
    return t, static

for i,r in enumerate(md.Field.rows):
    t,static = parse_field_sig(r.Signature)
    print(f"F{i+1}: {r.Name} : {t} {'static' if static else 'instance'}")

print("\n=== Methods (RVA + sig) ===")
def parse_method_sig(blob):
    b=blob_bytes(blob)
    i=0
    cc=b[i]; i+=1
    genarity=0
    if cc & 0x10:
        genarity=b[i]|(b[i+1]<<8); i+=2
    paramcount=b[i]; i+=1
    def read_type(i):
        et=b[i]; i+=1
        if et==0x0E: return 'string',i
        if et==0x01: return 'void',i
        if et==0x08: return 'object',i
        if et==0x0C: return 'int32',i
        if et==0x02: return 'bool',i
        if et in (0x0D,0x18): return 'byref('+ (read_type(i)[0]) +')', read_type(i)[1]
        if et==0x11:
            td=(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24))>>1
            which=td&1; tok=td>>1; i+=4
            return 'value:'+type_name((0x02000000|tok) if which==0 else (0x01000000|tok)),i
        if et==0x12:
            td=(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24))>>1
            which=td&1; tok=td>>1; i+=4
            return 'class:'+type_name((0x02000000|tok) if which==0 else (0x01000000|tok)),i
        if et==0x1D:
            td=(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24))>>1
            which=td&1; tok=td>>1; i+=4
            tn=type_name((0x02000000|tok) if which==0 else (0x01000000|tok))
            argc=b[i]; i+=1
            args=[]
            for _ in range(argc):
                a,i=read_type(i); args.append(a)
            return tn+'<'+','.join(args)+'>',i
        if et==0x0F:
            a,i=read_type(i); return a+'*',i
        if et==0x15: # ptr
            a,i=read_type(i); return a+'*',i
        return f'et{et:02X}',i
    ret,i=read_type(i)
    params=[]
    for _ in range(paramcount):
        p,i=read_type(i); params.append(p)
    return ret,params,genarity

method_parents={}
# map methods to type via TypeDef.MethodList ranges
for tr,r in enumerate(md.Typedef.rows):
    start=r.MethodList.row if hasattr(r.MethodList,'row') else r.MethodList
    end=len(md.Method.rows)+1
    if tr+1 < len(md.Typedef.rows):
        nr=md.Typedef.rows[tr+1]
        end=nr.MethodList.row if hasattr(nr.MethodList,'row') else nr.MethodList
    for m in range(start,end):
        method_parents[m]=f"{r.TypeNamespace}.{r.TypeName}" if r.TypeNamespace else r.TypeName

for i,r in enumerate(md.Method.rows):
    ret,params,ga=parse_method_sig(r.Signature)
    print(f"M{i+1} [{method_parents.get(i+1,'?')}] {r.Name} RVA=0x{r.Rva:08X} attrs={r.Attributes} : {ret} ({', '.join(params)}){' gen='+str(ga) if ga else ''}")

print("\n=== MemberRef ===")
for i,r in enumerate(md.MemberRef.rows):
    cls=tname(r.Class)
    try:
        b=blob_bytes(r.Signature)
        kind=b[0]
        desc='METHOD' if (kind&0xFF)==0x06 else 'FIELD'
    except Exception:
        desc='?'
    print(f"MR{i+1} (token 0x{0x0A000000+i+1:08X}): {cls}::{r.Name} [{desc}]")

print("\n=== TypeSpec ===")
for i,r in enumerate(md.TypeSpec.rows):
    b=blob_bytes(r.Signature)
    # crude decode of instantiation
    def rd(i):
        et=b[i]; i+=1
        if et==0x1D:
            td=(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24))>>1
            which=td&1; tok=td>>1; i+=4
            tn=type_name((0x02000000|tok) if which==0 else (0x01000000|tok))
            argc=b[i]; i+=1
            args=[]
            for _ in range(argc):
                a,i=rd(i); args.append(a)
            return tn+'<'+','.join(args)+'>',i
        if et==0x0C: return 'Int32',i
        if et==0x0E: return 'String',i
        if et==0x11:
            td=(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24))>>1
            which=td&1; tok=td>>1; i+=4
            return 'value:'+type_name((0x02000000|tok) if which==0 else (0x01000000|tok)),i
        if et==0x12:
            td=(b[i]|(b[i+1]<<8)|(b[i+2]<<16)|(b[i+3]<<24))>>1
            which=td&1; tok=td>>1; i+=4
            return 'class:'+type_name((0x02000000|tok) if which==0 else (0x01000000|tok)),i
        if et==0x02: return 'Boolean',i
        if et==0x1B: # generic param
            n=b[i]|(b[i+1]<<8); i+=2
            return f'!{n}',i
        if et==0x20: # array
            a,i=rd(i)
            argc=b[i]; i+=1
            for _ in range(argc): 
                lo=b[i]|(b[i+1]<<8); hi=b[i+2]|(b[i+3]<<8); i+=4
            return a+'[]',i
        return f'et{et:02X}',i
    s,_=rd(0)
    print(f"TS{i+1} (token 0x{0x1B000000+i+1:08X}): {s} raw={b.hex()}")

print("\n=== Custom Attributes ===")
for i,r in enumerate(md.CustomAttribute.rows):
    at=type_name(r.Type) if not isinstance(r.Type,int) else type_name(r.Type)
    par=tname(r.Parent)
    try:
        vals=md.parse_custom_attribute if False else None
    except Exception: pass
    blob=blob_bytes(r.Value)
    print(f"CA{i+1}: {par} <- {at} blob={blob.hex()}")

print("\n=== US heap (proper) ===")
us=pe.net.metadata.streams['#US']
data=us.Data if hasattr(us,'Data') else us.data
off=1
while off < len(data):
    # var-int length
    b0=data[off]
    if b0==0: off+=1; continue
    if b0 & 0x80:
        l=((b0&0x7f)<<8)|data[off+1]; off+=2
    else:
        l=b0; off+=1
    raw=data[off:off+l]
    if l>=2 and (data[off+l-1]&1)==0:
        try: s=raw[:-1].decode('utf-16-le')
        except Exception: s=repr(raw)
        print(f"0x70{off:06X}: {s!r}")
    off+=l
