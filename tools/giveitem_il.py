import dnfile
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBase

DLL = '/root/.nuget/packages/riskofrain2.gamelibs/1.5.0-r.1/lib/netstandard2.0/RoR2.dll'
pe = dnfile.dnPE(DLL)
md = pe.net.mdtables
td_rows = md.TypeDef.rows
md_rows = md.MethodDef.rows
tr_rows = md.TypeRef.rows

def s(row, attr):
    v = getattr(row, attr)
    return str(v.value) if hasattr(v, 'value') else str(v)

def idx(row, attr):
    v = getattr(row, attr)
    if isinstance(v, list): return v[0].row_index
    if hasattr(v, 'value'): return v.value
    return int(v)

class R(CilMethodBodyReaderBase):
    def __init__(self, data, off): self.data=data; self.off=off
    def read(self, n):
        b = self.data[self.off:self.off+n]; self.off += len(b); return b
    def tell(self): return self.off
    def seek(self, o): self.off = o

def rva_to_off(rva):
    for sec in pe.sections:
        va = sec.VirtualAddress
        vs = max(sec.Misc_VirtualSize, sec.SizeOfRawData)
        if va <= rva < va + vs:
            return sec.PointerToRawData + (rva - va)
    return None

def memberref_desc(tok):
    mr = md.MemberRef.rows[(tok & 0xFFFFFF) - 1]
    nm = s(mr,'Name')
    try:
        enc = mr.Class
        tbl, rid = enc.table, enc.row_index
        if tbl == 0x01: cls = s(tr_rows[rid-1],'TypeNamespace')+'.'+s(tr_rows[rid-1],'TypeName')
        elif tbl == 0x02: cls = s(td_rows[rid-1],'TypeNamespace')+'.'+s(td_rows[rid-1],'TypeName')
        else: cls = '?'
    except Exception: cls='?'
    return cls + '.' + nm

def tokdesc(tv):
    t = tv >> 24; rid = (tv & 0xFFFFFF) - 1
    if t == 0x0A: return ' -> ' + memberref_desc(tv)
    if t == 0x01: return ' type=' + s(tr_rows[rid],'TypeNamespace')+'.'+s(tr_rows[rid],'TypeName')
    if t == 0x02: return ' type=' + s(td_rows[rid],'TypeNamespace')+'.'+s(td_rows[rid],'TypeName')
    if t == 0x06: return ' methoddef#'+str(rid+1)+' '+s(md_rows[rid],'Name')
    if t == 0x04:
        fr = md.Field.rows[rid]
        return ' field '+s(fr,'Name')
    if t == 0x28: return ' methodspec#'+str(rid+1)
    return ' tok='+hex(tv)

for i, td in enumerate(td_rows):
    if s(td,'TypeName')=='Inventory' and s(td,'TypeNamespace')=='RoR2':
        start = idx(td,'MethodList')-1
        end = idx(td_rows[i+1],'MethodList')-1
        break

data = open(DLL,'rb').read()
out = []
for m in md_rows[start:end]:
    nm = s(m,'Name')
    if nm not in ('GiveItem','GiveItemPermanent','GiveItemTemp','DiscoverPickup'): continue
    rva = m.Rva
    if rva == 0:
        out.append(f"### {nm} : NO BODY RVA=0")
        continue
    off = rva_to_off(rva)
    reader = R(data, off)
    try:
        body = CilMethodBody(reader)
    except Exception as e:
        out.append(f"### {nm}: parse error {e}")
        continue
    out.append(f"### {nm} (RVA {hex(rva)})")
    for instr in body.instructions:
        opc = instr.opcode
        line = "  " + str(opc.name)
        tokv = getattr(instr, 'token', None)
        val = getattr(instr, 'value', None)
        extra = ''
        if tokv is not None:
            extra = tokdesc(int(tokv))
        elif val is not None:
            extra = ' ' + str(val)
        out.append(line + extra)

open('/tmp/giveitem_il.txt','w').write('\n'.join(out))
print("wrote", len(out), "lines")
