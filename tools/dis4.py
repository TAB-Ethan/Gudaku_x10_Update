import dnfile, struct
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBase

pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
md = pe.net.mdtables
data = pe.__data__
foff = data.find(b'BSJB')
mh = data[foff:]
p = 4+4+2+2
plen = struct.unpack('<I', mh[p:p+4])[0]; p += 4
p += plen
p += 2
nstreams = struct.unpack('<H', mh[p:p+2])[0]; p += 2
us_raw = None
for i in range(nstreams):
    off, size = struct.unpack('<II', mh[p:p+8]); p += 8
    name = b''
    while mh[p] != 0:
        name += bytes([mh[p]]); p += 1
    p += 1
    p += (4-(p % 4)) % 4
    if name == b'#US':
        us_raw = data[foff+off:foff+off+size]

def us_string(offset):
    b = us_raw[offset]
    if b < 0x80: n=b; start=offset+1
    elif b < 0xC0: n=((b&0x7F)<<8)|us_raw[offset+1]; start=offset+2
    else: n=((b&0x3F)<<24)|(us_raw[offset+1]<<16)|(us_raw[offset+2]<<8)|us_raw[offset+3]; start=offset+4
    bs = us_raw[start:start+n]
    if n & 1: bs = bs[:-1]
    try: return bs.decode('utf-16-le')
    except Exception: return bs.hex()

def rva_to_off(rva):
    for s in pe.sections:
        if s.VirtualAddress <= rva < s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData):
            return s.PointerToRawData + (rva - s.VirtualAddress)
    return None

tr = md.TypeRef.rows; tds = md.TypeDef.rows; mrs = md.MemberRef.rows
frs = md.Field.rows; mps = md.MethodDef.rows

def sval(f):
    v = getattr(f, 'value', f); return '' if v is None else str(v)

def typeref_name(idx):
    r = tr[idx-1]
    ns = sval(r.TypeNamespace); nm = sval(r.TypeName)
    return (ns+'.' if ns else '')+nm

def codedname(cdi):
    tag = cdi & 3; row = cdi >> 2
    if tag == 0:
        r = tds[row]; ns = sval(r.TypeNamespace); nm = sval(r.TypeName)
        return (ns+'.' if ns else '')+nm
    if tag == 1: return typeref_name(row)
    return f"TypeSpec#{row}"

def memberref_desc(idx):
    r = mrs[idx-1]
    cls = r.Class
    val = cls.value if hasattr(cls, 'value') else cls
    parent = codedname(val) if isinstance(val, int) else str(val)
    sig = bytes(r.Signature or b"")
    return parent+"::"+sval(r.Name)+" sig="+sig.hex()

def field_desc(idx):
    r = frs[idx-1]
    owner = r.Parent
    val = owner.value if hasattr(owner, 'value') else owner
    oname = codedname(val) if isinstance(val, int) else str(val)
    return oname+"."+sval(r.Name)

class Rd(CilMethodBodyReaderBase):
    def __init__(self, off): self.data = data; self.pos = off
    def read(self, n):
        b = self.data[self.pos:self.pos+n]; self.pos += n; return b
    def tell(self): return self.pos
    def seek(self, o): self.pos = o
    def get_token_string(self, token):
        t = token.table; rid = token.rid
        if t == 0x70: return 'US:%r' % us_string(rid)
        if t == 0x0A: return 'MR%d[%s]' % (rid, memberref_desc(rid))
        if t == 0x01: return 'TR[%s]' % typeref_name(rid)
        if t == 0x04: return 'FLD[%s]' % field_desc(rid)
        if t == 0x06: return 'M%d[%s]' % (rid, sval(mps[rid-1].Name))
        if t == 0x1B: return 'TS#%d' % rid
        return repr(token)

for mi, mrow in enumerate(mps, 1):
    print(f"===== M{mi} {sval(mrow.Name)} =====")
    off = rva_to_off(mrow.Rva)
    if off is None: print("no rva"); continue
    try:
        body = CilMethodBody(Rd(off))
    except Exception as e:
        print("ERR", e); continue
    for ins in body.instructions:
        line = "  " + ins.opcode.name
        o = ins.operand
        if o is not None:
            line += " " + str(o)
        print(line)
    for eh in (body.exception_handlers or []):
        print("  EH:", eh.kind, getattr(eh, 'catch_type', None))
