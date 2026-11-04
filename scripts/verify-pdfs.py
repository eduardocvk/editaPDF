from pypdf import PdfReader
from pathlib import Path
import sys
p=Path(sys.argv[1])
original=PdfReader(p/'fixtures'/'externo.pdf'); edited=PdfReader(p/'externo-editado.pdf')
assert len(original.pages)==len(edited.pages)==2
assert 'Edicion real 456' in edited.pages[0].extract_text()
assert 'Texto original compatible 123' not in edited.pages[0].extract_text()
assert 'Contenido independiente' in edited.pages[0].extract_text()
assert original.pages[1].extract_text()==edited.pages[1].extract_text()
assert len(edited.pages[0]['/Contents'].get_object().get_data())>0
complex_before=PdfReader(p/'fixtures'/'estructuras.pdf'); complex_after=PdfReader(p/'estructuras-editado.pdf')
assert len(complex_after.outline)==len(complex_before.outline)==1
assert list(complex_after.attachments)==list(complex_before.attachments)
assert complex_after.attachments['nota.txt'][0]==complex_before.attachments['nota.txt'][0]
assert complex_after.metadata.title==complex_before.metadata.title
assert len(complex_after.pages[0]['/Annots'])==1
dest=complex_after.pages[0]['/Annots'][0].get_object()['/Dest'][0]
assert dest.idnum==complex_after.pages[1].indirect_reference.idnum
assert 'Edicion real 456' in complex_after.pages[0].extract_text()
print('PASS verificacion independiente pypdf: contenido, paginas, marcador, adjunto, metadatos y destino interno')
def check_annotations(before_page,after_page):
    before=before_page.get('/Annots',[]);after=after_page.get('/Annots',[])
    assert len(before)==len(after)
    for b,a in zip(before,after):
        b=b.get_object();a=a.get_object()
        for key in ['/Subtype','/Rect','/Contents','/T','/NM','/F']:
            assert b.get(key)==a.get(key),(key,b.get(key),a.get(key))
        if b.get('/A',{}).get('/S')=='/URI':assert a['/A']['/URI']==b['/A']['/URI']
        if '/AP' in b:assert b['/AP']['/N'].get_object().get_data()==a['/AP']['/N'].get_object().get_data()
for name in ['estructuras','anotaciones','destinos']:
    before=PdfReader(p/'fixtures'/f'{name}.pdf');after=PdfReader(p/f'unido-{name}.pdf')
    assert len(after.pages)==len(before.pages)+2
    for i,page in enumerate(before.pages):
        assert page.extract_text()==after.pages[i].extract_text()
        check_annotations(page,after.pages[i])
    if name=='estructuras':
        assert after.get_destination_page_number(after.outline[0])==0
        assert after.attachments['nota.txt'][0]==before.attachments['nota.txt'][0]
        dest=after.pages[0]['/Annots'][0].get_object()['/Dest'][0]
        assert dest.idnum==after.pages[1].indirect_reference.idnum
    if name=='destinos':assert after.get_destination_page_number(after.named_destinations['destino-sintetico'])==1
    print('PASS pypdf union: referencias y contenido conservados con',name)
before=PdfReader(p/'fixtures'/'anotaciones.pdf');after=PdfReader(p/'importado-anotaciones.pdf')
for i,page in enumerate(before.pages):check_annotations(page,after.pages[i+2])
print('PASS pypdf importacion de anotaciones: nota, URI y apariencia del sello conservadas')
rendered=PdfReader(p/'visualizado-anotaciones.pdf')
for i,page in enumerate(before.pages):check_annotations(page,rendered.pages[i])
print('PASS pypdf visualizacion: posiciones originales de notas y apariencias intactas despues de renderizar')
