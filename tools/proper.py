import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
import dnfile
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBase
from dncil.clr.token import Token

pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
md = pe.net.mdtables
def bb(x):
    v=x.value
    if callable(v): v=v()
    return bytes(v)
def coded(o):
    if isinstance(o, list):
        # range of rows: [start_rowobj,...]
        try:
            first=o[0]
            ri=getattr(first,'row_index',None)
            return ('RANGE', (ri+1 if ri is not None else '?', len(o)))
        except Exception:
            return ('RANGE', len(o))
    t=o.table; tn=getattr(t,'__name__',None) or str(t)
    r=o.row; ri=getattr(r,'row_index',None)
    return (tn, ri+1 if ri is not None else r)
def rangeend(cell):
    if cell is None: return None
    nxt = cell.row.next_row_index if hasattr(cell.row,'next_row_index') and cell.row.next_row_index is not None else None
    if nxt is not None: return nxt + 1
    return None

st=[s for s in pe.net.metadata.streams_list if type(s).__name__=='UserStringHeap'][0]
fo=st.file_offset
blob=pe.__data__[fo:]
US={}
i=0
while i < len(blob):
    b0=blob[i]
    if b0==0: i+=1; continue
    if b0 & 0xC0 == 0x80: slen=((b0&0x3F)<<8)|blob[i+1]; hdr=2
    elif b0 & 0xC0 == 0xC0: slen=((b0&0x3F)<<24)|(blob[i+1]<<16)|(blob[i+2]<<8)|blob[i+3]; hdr=4
    else: slen=b0; hdr=1
    data=blob[i+hdr:i+hdr+slen]
    try: s=data.decode('utf-16-le')
    except Exception: s=None
    US[i]=s
    i += hdr+slen+1

def trname(i):
    r=md.TypeRef.rows[i-1]
    stt,sr=coded(r.ResolutionScope)
    sn=''
    if stt=='AssemblyRef': sn=md.AssemblyRef.rows[sr-1].Name
    elif stt=='TypeDef': sn=f"TD{sr}"
    return f"{sn}|{r.TypeNamespace}.{r.TypeName}"
TRS=[trname(i+1) for i in range(len(md.TypeRef.rows))]

def memberdesc(tok):
    hi=tok.table; rid=tok.rid
    if hi==0x0A:
        r=md.MemberRef.rows[rid-1]
        ct,cr=coded(r.Class)
        if ct=='TypeRef': cn=TRS[cr-1]
        elif ct=='TypeSpec': cn=f"TS{cr}[{bb(md.TypeSpec.rows[cr-1].Signature).hex()}]"
        elif ct=='ModuleRef': cn=f"ModRef:{md.ModuleRef.rows[cr-1].Name}"
        else: cn=f"{ct}{cr}"
        return f"{cn}::{r.Name}"
    if hi==0x06: return f"this.{md.MethodDef.rows[rid-1].Name}"
    if hi==0x04: return f"this.F{rid}:{md.Field.rows[rid-1].Name}"
    if hi==0x01: return f"this.TD{rid}:{md.TypeDef.rows[rid-1].TypeName}"
    if hi==0x1B: return f"TS{rid}[{bb(md.TypeSpec.rows[rid-1].Signature).hex()}]"
    if hi==0x2B:
        r=md.MethodSpec.rows[rid-1]
        mt,mrr=coded(r.Method)
        pfx={'MemberRef':0x0A,'MethodDef':0x06}.get(mt)
        base=memberdesc(Token(pfx<<24|mrr)) if pfx else f"{mt}{mrr}"
        inst=bb(r.Instantiation); ts=[]; j=1
        while j<len(inst):
            t=inst[j]
            if t==0x08: ts.append('bool'); j+=1
            elif t==0x1c: ts.append('object'); j+=1
            elif t==0x0e: ts.append('int'); j+=1
            elif t==0x12:
                val=(inst[j+1]|(inst[j+2]<<8)|(inst[j+3]<<16)|(inst[j+4]<<24))>>1
                tbl=val>>24; rr=val&0xFFFFFF
                if tbl==0x1b: ts.append(f"TS{rr}")
                elif tbl==0x01: ts.append(f"TD{rr}:{md.TypeDef.rows[rr-1].TypeName}")
                elif tbl==0x00: ts.append(f"TR{rr}:{trs_short(rr)}")
                else: ts.append(f"tok{tbl:02X}{rr}")
                j+=5
            else: ts.append(f"?{t:02x}"); j+=1
        return f"GEN<{base}>({','.join(ts)})"
    return f"tok{int(tok):08X}"

def trs_short(i):
    r=md.TypeRef.rows[i-1]
    return f"{r.TypeNamespace}.{r.TypeName}"

class R(CilMethodBodyReaderBase):
    def __init__(self,data,off): self.d=data; self.o=off; super().__init__()
    def tell(self): return self.o
    def seek(self,offset): self.o=offset
    def read(self,length):
        v=self.d[self.o:self.o+length]; self.o+=length; return v

sec=pe.sections
def rva_to_off(rva):
    for s in sec:
        if s.VirtualAddress<=rva<s.VirtualAddress+max(s.Misc_VirtualSize,s.SizeOfRawData):
            return rva-s.VirtualAddress+s.PointerToRawData
    return None

print("=== TypeDefs ===")
for i,r in enumerate(md.TypeDef.rows):
    print(f"TD{i+1}: ns={r.TypeNamespace!r} name={r.TypeName!r} flds={coded(r.FieldList)} meths={coded(r.MethodList)} nested={coded(r.NestedClassRow) if getattr(r,'NestedClassRow',None) else None} flags={str(r.Flags)[:70]}")

print("\n=== Fields ===")
for i,r in enumerate(md.Field.rows): print(f"F{i+1}: {r.Name} sig={bb(r.Signature).hex()} flags={str(r.Flags)[:40]}")

print("\n=== CustomAttribute ===")
for i,r in enumerate(md.CustomAttribute.rows):
    pt,pr=coded(r.Parent); ct,cr=coded(r.Type)
    print(f"CA{i+1}: parent=({pt},{pr}) type=({ct},{cr}) val={bb(r.Value).hex()}")

print("\n=== AssemblyRef ===")
for i,r in enumerate(md.AssemblyRef.rows): print(f"AR{i+1}: {r.Name} v{r.MajorVersion}.{r.MinorVersion}.{r.BuildNumber}.{r.RevisionNumber}")

print("\n=== Methods ===")
for i,m in enumerate(md.MethodDef.rows):
    off=rva_to_off(m.Rva)
    print(f"\n===== M{i+1} {m.Name} =====")
    try:
        body=CilMethodBody(R(pe.__data__,off))
        for ins in body.instructions:
            opname=str(ins.opcode.name) if hasattr(ins.opcode,'name') else str(ins.opcode)
            o=ins.operand
            extra=''
            if isinstance(o,Token):
                if o.table==0x75: extra=f' STRING {US.get(o.rid)!r}'
                else: extra=' '+memberdesc(o)
            elif o is not None: extra=f' {o}'
            print(f"  {ins.offset:04X}: {opname}{extra}")
    except Exception as e:
        print("  ERR",repr(e))
