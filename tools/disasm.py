import dnfile
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBase
from dncil.clr.token import Token

pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
md = pe.net.mdtables

class RefStringer(CilMethodBodyReaderBase):
    def __init__(self, data, offset):
        self.data = data
        self.pos = offset
    def read(self, count):
        b = self.data[self.pos:self.pos+count]
        self.pos += count
        return b
    def tell(self):
        return self.pos
    def seek(self, offset):
        self.pos = offset
    def get_token_string(self, token):
        table_index = token.table
        rid = token.rid - 1
        try:
            if table_index == 0x70:
                return f'"{md.StringHeap.get_string(rid)}"'
            t = {0x0A: md.MemberRef, 0x01: md.TypeRef, 0x04: md.Field, 0x06: md.MethodDef,
                 0x02: md.TypeDef}.get(table_index)
            if t is not None:
                r = t.rows[rid]
                nm = getattr(r,'Name',None) or getattr(r,'TypeName',None)
                parent = ''
                try:
                    prow = r.Class.row
                    pn = getattr(prow,'TypeName',None)
                    if pn:
                        ns = getattr(prow,'TypeNamespace',None)
                        nsv = (ns.value + '.') if ns and ns.value else ''
                        parent = nsv + pn.value + '.'
                except Exception:
                    pass
                return f"{parent}{nm.value}"
            return f"tok:{token!r}"
        except Exception as e:
            return f"tok:{token!r}"

def rva_to_off(rva):
    for s in pe.sections:
        if s.VirtualAddress <= rva < s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData):
            return s.PointerToRawData + (rva - s.VirtualAddress)
    raise ValueError(hex(rva))

methods = [(m.Name.value, m.Rva) for m in md.MethodDef.rows]
for name, rva in methods:
    off = rva_to_off(rva)
    reader = RefStringer(pe.__data__, off); body = CilMethodBody(reader)
    print(f"===== {name} =====")
    for instr in body.instructions:
        op = instr.opcode.name
        operand = ""
        if instr.operand is not None:
            if isinstance(instr.operand, Token):
                operand = " ; " + reader.get_token_string(instr.operand)
            else:
                operand = f" ({instr.operand})"
        print(f"{op}{operand}")
