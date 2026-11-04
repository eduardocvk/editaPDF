"""Read-only PE import check; no network access or third-party packages."""
from pathlib import Path
import struct, sys
p=Path(sys.argv[1]); data=p.read_bytes()
u16=lambda o:struct.unpack_from('<H',data,o)[0]
u32=lambda o:struct.unpack_from('<I',data,o)[0]
pe=u32(0x3c);opt=pe+24;sections=opt+u16(pe+20)
def offset(rva):
    for i in range(u16(pe+6)):
        s=sections+i*40;start=u32(s+12)
        if start<=rva<start+max(u32(s+8),u32(s+16)):return rva-start+u32(s+20)
    raise ValueError('Unmapped RVA')
directory=opt+(112 if u16(opt)==0x20b else 96)
imports=[]
for relative,step,name_at in [(8,20,12),(13*8,32,4)]:
    rva=u32(directory+relative)
    if not rva:continue
    o=offset(rva)
    while any(data[o:o+step]):
        n=offset(u32(o+name_at));end=data.index(0,n);imports.append(data[n:end].decode('ascii'));o+=step
print(p.name+': '+', '.join(imports))
network={'ws2_32.dll','mswsock.dll','winhttp.dll','wininet.dll','dnsapi.dll','urlmon.dll'}
assert not network.intersection(n.lower() for n in imports), 'Network dependency found'
assert not any('vcruntime140.dll'==n.lower() or n.lower().startswith('msvcp140') for n in imports), 'Unbundled VC runtime dependency'
print('PASS native imports: no direct networking or external VC runtime')
