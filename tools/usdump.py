import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
import dnfile
pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
us = pe.net.user_strings
raw = bytes(us.__data__)
# walk entries: compressed uint length + utf16 data + 1 terminal byte
def cu(b,i):
    x=b[i]
    if x<0x80: return x,i+1
    if (x&0xC0)==0x80: return ((x&0x3F)<<8)|b[i+1], i+2
    return ((x&0x3F)<<24)|(b[i+1]<<16)|(b[i+2]<<8)|b[i+3], i+4
i=0; idx=0
while i < len(raw):
    slen, ni = cu(raw,i)
    if slen==0 and raw[i]==0:
        i+=1; continue
    data=raw[ni:ni+slen]
    term=raw[ni+slen] if ni+slen<len(raw) else 0
    try: s=data.decode('utf-16-le')
    except Exception as e: s=f"<decode err {e}>"
    print(f"US{idx}@{i}: len={slen} term={term} :: {s!r}")
    idx+=1
    i = ni+slen+1
