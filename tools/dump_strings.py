import dnfile, struct
pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
data = pe.__data__
# find the stream named '#Strings' after 'BSJB' and parse the stream table properly
idx = data.find(b'#Strings')
# step back to start of its stream header (offset(4)+size(4)+name)
q = idx - 8
# number of streams: scan backwards to find metadata header is hard; instead parse forward from a known point.
# The metadata root: signature BSJB at some offset mroot. Stream headers begin after version+2 shorts.
# Simpler approach: locate all stream names by scanning known names in order.
names_order = []
def read_stream_at(pos):
    off, size = struct.unpack('<II', data[pos:pos+8])
    nm = data[pos+8:data.index(b'\x00', pos+8)].decode()
    return nm, off, size
# reconstruct: find 'BSJB'
mroot = None
scan = 0
# Use dnfile's parsed values instead: pe.net.metadata.struct? Try attributes on md streams objects
mds = pe.net.metadata.streams_list
sh = None
ush = None
for s in mds:
    if type(s).__name__ == 'StringsHeap':
        sh = s
    elif type(s).__name__ == 'UserStringHeap':
        ush = s

def gs(offset):
    return sh.get_str(offset)

tokens = [0x70000001,0x70000009,0x70000021,0x70000037,0x70000093,0x700000BB,0x70000312,0x7000033C,
0x70000409,0x70000425,0x7000047F,0x70000540,0x7000059A,0x700005EC,0x7000063E,0x70000642,0x700007CB,
0x70000819,0x7000091E]
print("=== #Strings tokens ===")
for t in tokens:
    print(hex(t), '->', repr(gs(t & 0xFFFFFF)))

print("=== #US strings ===")
us_tokens = [0x70000001,0x70000009,0x70000021,0x70000037,0x70000093,0x700000BB,0x70000312,0x7000033C,
0x70000409,0x70000425,0x7000047F,0x70000540,0x7000059A,0x700005EC,0x7000063E,0x70000642,0x700007CB,
0x70000819,0x7000091E]
for t in us_tokens:
    try:
        print(hex(t), '->', repr(ush.get(t & 0xFFFFFF)))
    except Exception as e:
        print(hex(t), 'ERR', e)

md = pe.net.mdtables
print("=== TypeRef rows ===")
for i,r in enumerate(md.TypeRef.rows):
    ns = r.TypeNamespace.value if r.TypeNamespace else ''
    tn = r.TypeName.value if r.TypeName else ''
    print(i+1, ns, tn)
print("=== MemberRef rows ===")
for i,r in enumerate(md.MemberRef.rows):
    nm = r.Name.value if r.Name else ''
    try:
        prow = r.Class.row
        pn = getattr(prow,'TypeName',None)
        pv = ''
        if pn:
            ns = getattr(prow,'TypeNamespace',None)
            pv = ((ns.value+'.') if ns and ns.value else '') + pn.value
    except Exception:
        pv = str(r.Class)
    print(i+1, pv, nm)
print("=== AssemblyRef rows ===")
for i,r in enumerate(md.AssemblyRef.rows):
    print(i+1, r.Name.value, r.Version)
print("=== CustomAttribute type rows ===")
for i,r in enumerate(md.CustomAttribute.rows):
    try:
        t = r.Type.row
        if hasattr(t, 'Name'):
            nm = t.Name.value if t.Name else '?'
            par = getattr(t, 'Class', None)
            try:
                pr = par.row
                nm = (getattr(pr,'TypeName',None).value if getattr(pr,'TypeName',None) else '?') + '.' + nm
            except Exception:
                pass
        else:
            nm = str(t)
    except Exception as e:
        nm = 'ERR '+str(e)
    print(i+1, nm)
