import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
import dnfile
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBase
from dncil.clr.token import Token

pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
md = pe.net.mdtables

def bs(x):
    if x is None: return ''
    v=x.value
    if callable(v): v=v()
    if isinstance(v,bytes): return v.decode('utf-8','replace')
    return str(v)
def bb(x):
    v=x.value
    if callable(v): v=v()
    return bytes(v)
def coded(o):
    t=o.table; tn=getattr(t,'__name__',None) or str(t)
    r=o.row; ri=getattr(r,'row_index',None)
    return (tn, ri+1 if ri is not None else '?')

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
    if stt=='AssemblyRef': sn=bs(md.AssemblyRef.rows[sr-1].Name)
    return f"{sn}|{bs(r.TypeNamespace)}.{bs(r.TypeName)}"
TRS=[trname(i+1) for i in range(len(md.TypeRef.rows))]

def mrdesc(rid):
    r=md.MemberRef.rows[rid-1]
    ct,cr=coded(r.Class)
    if ct=='TypeRef': cn=TRS[cr-1]
    elif ct=='TypeSpec': cn=f"TS{cr}[{bb(md.TypeSpec.rows[cr-1].Signature).hex()}]"
    elif ct=='ModuleRef': cn=f"ModRef:{bs(md.ModuleRef.rows[cr-1].Name)}"
    else: cn=f"{ct}{cr}"
    return f"{cn}::{bs(r.Name)}"

def tokint(o):
    try: return int(o)
    except TypeError: pass
    import re
    m=re.search(r'0x([0-9a-fA-F]+)',str(o))
    if m: return int(m.group(1),16)
    raise ValueError(str(o))

def memberdesc(tokval):
    hi=(tokval>>24)&0xFF; rid=tokval&0xFFFFFF
    if hi==0x0A: return mrdesc(rid)
    if hi==0x06: return f"this.{bs(md.MethodDef.rows[rid-1].Name)}"
    if hi==0x04: return f"this.F{rid}:{bs(md.Field.rows[rid-1].Name)}"
    if hi==0x01: return f"this.TD{rid}:{bs(md.TypeDef.rows[rid-1].TypeName)}"
    if hi==0x02: return f"TypeRef {TRS[rid-1]}"
    if hi==0x1B: return f"TS{rid}[{bb(md.TypeSpec.rows[rid-1].Signature).hex()}]"
    if hi==0x2B:
        r=md.MethodSpec.rows[rid-1]
        mt,mrr=coded(r.Method)
        pfx={'MemberRef':0x0A,'MethodDef':0x06}.get(mt)
        base=memberdesc(pfx<<24|mrr) if pfx else f"{mt}{mrr}"
        inst=bb(r.Instantiation); ts=[]; j=1
        while j<len(inst):
            t=inst[j]
            if t==0x08: ts.append('bool'); j+=1
            elif t==0x1c: ts.append('object'); j+=1
            elif t==0x0e: ts.append('int32'); j+=1
            elif t in (0x12,0x11,0x07):
                val=(inst[j+1]|(inst[j+2]<<8)|(inst[j+3]<<16)|(inst[j+4]<<24))>>1
                tbl=val>>24; rr=val&0xFFFFFF
                if tbl==0x1b: ts.append(f"TS{rr}[{bb(md.TypeSpec.rows[rr-1].Signature).hex()}]")
                elif tbl==0x01: ts.append(f"TD{rr}")
                elif tbl==0x00: ts.append(f"TR{rr}")
                else: ts.append(f"tok{tbl:02X}{rr}")
                j+=5
            else: ts.append(f"?{t:02x}"); j+=1
        return f"GEN<{base}>({','.join(ts)})"
    return f"tok{tokval:08X}"

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
    print(f"TD{i+1}: ns={bs(r.TypeNamespace)!r} name={bs(r.TypeName)!r}")
print("\n=== AssemblyRef ===")
for i,r in enumerate(md.AssemblyRef.rows): print(f"AR{i+1}: {bs(r.Name)}")
print("\n=== Fields ===")
for i,r in enumerate(md.Field.rows): print(f"F{i+1}: {bs(r.Name)} sig={bb(r.Signature).hex()}")
print("\n=== CustomAttribute ===")
for i,r in enumerate(md.CustomAttribute.rows):
    pt,pr=coded(r.Parent); ct,cr=coded(r.Type)
    att=mrdesc(cr) if ct=='MemberRef' else f"{ct}{cr}"
    val=bb(r.Value)
    printable=''.join(chr(b) if 32<=b<127 else '.' for b in val)
    print(f"CA{i+1}: on {pt}{pr} attr={att} :: {printable}")

print("\n=== Methods ===")
for i,m in enumerate(md.MethodDef.rows):
    off=rva_to_off(m.Rva)
    print(f"\n===== M{i+1} {bs(m.Name)} =====")
    body=CilMethodBody(R(pe.__data__,off))
    for ins in body.instructions:
        opname=str(ins.opcode.name) if hasattr(ins.opcode,'name') else str(ins.opcode)
        o=ins.operand
        extra=''
        try:
            if o is not None and (isinstance(o,Token) or 'Token' in type(o).__name__):
                tv=tokint(o)
                hi=(tv>>24)&0xFF
                if hi==0x75: extra=f' STRING {US.get(tv&0xFFFFFF)!r}'
                else: extra=' '+memberdesc(tv)
            elif o is not None:
                extra=f' {o}'
        except Exception as e:
            extra=f' [descERR {type(e).__name__}]'
        print(f"  {ins.offset:04X}: {opname}{extra}")
