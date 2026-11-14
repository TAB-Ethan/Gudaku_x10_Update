import dnfile, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pe = dnfile.dnPE('/workspace/Gudako-x10.dll')
for sec in pe.net.metadata.streams_list:
    print(type(sec).__name__, getattr(sec,'name',None))
