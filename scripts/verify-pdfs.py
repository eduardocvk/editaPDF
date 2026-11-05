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
reordered=PdfReader(p/'reordenado-estructuras.pdf')
assert reordered.get_destination_page_number(reordered.outline[0])==1
assert reordered.pages[1]['/Annots'][0].get_object()['/Dest'][0].idnum==reordered.pages[0].indirect_reference.idnum
for name in ['estructuras','enlace-interno','destinos','formulario','etiquetado']:
    merged=PdfReader(p/f'mezcla-{name}.pdf');assert len(merged.pages)==4
    if name=='estructuras':
        assert merged.get_destination_page_number(merged.outline[0])==0
        assert merged.attachments['nota.txt'][0]==b'Adjunto sintetico local'
    if name in ['estructuras','enlace-interno']:assert merged.pages[0]['/Annots'][0].get_object()['/Dest'][0].idnum==merged.pages[3].indirect_reference.idnum
    if name=='destinos':assert merged.get_destination_page_number(merged.named_destinations['destino-sintetico'])==3
    if name=='formulario':assert merged.get_fields()['campo']['/V']=='Valor de prueba'
    if name=='etiquetado':
        root=merged.trailer['/Root']['/StructTreeRoot'];assert root['/ParentTree']['/Nums']
        def tag_pages(node):
            if isinstance(node,list):return sum((tag_pages(n) for n in node),[])
            node=node.get_object() if hasattr(node,'get_object') else node
            if not isinstance(node,dict):return []
            result=[merged.get_page_number(node['/Pg'])] if '/Pg' in node else []
            return result+tag_pages(node.get('/K',[]))
        assert set(tag_pages(root))=={0,3}
    print('PASS pypdf importar y reordenar:',name)
forms=PdfReader(p/'formularios-dobles.pdf').get_fields();assert set(forms)=={'campo','campo_2'} and all(f['/V']=='Valor de prueba' for f in forms.values())
dest=PdfReader(p/'destinos-dobles.pdf');assert len(dest.named_destinations)==2 and sorted(dest.get_destination_page_number(d) for d in dest.named_destinations.values())==[1,3]
print('PASS colisiones: formularios y destinos independientes, sin sobrescribir valores')
named=PdfReader(p/'destinos-enlazados-dobles.pdf');assert named.get_destination_page_number(named.outline[0])==3
name=str(named.pages[2]['/Annots'][0].get_object()['/A']['/D']);assert named.get_destination_page_number(named.named_destinations[name])==3
attachments=PdfReader(p/'adjuntos-dobles.pdf').attachments;assert attachments['nota.txt'][0]==b'Adjunto sintetico local' and attachments['nota (2).txt'][0]==b'Otro adjunto sintetico'
print('PASS colisiones: enlaces y marcadores apuntan a su documento; adjuntos con mismo nombre conservan ambos contenidos')
for name in ['protegido-editable','firma-sintetica-editable','con-contrasena-editable','firma-real-editable']:
    copy=PdfReader(p/f'{name}.pdf');assert not copy.is_encrypted and len(copy.pages)==2
    assert '/Perms' not in copy.trailer['/Root']
    assert not any(field.get('/FT')=='/Sig' for field in (copy.get_fields() or {}).values())
    assert any('Texto original compatible 123' in page.extract_text() for page in copy.pages)
    assert any('Segunda pagina conservada' in page.extract_text() for page in copy.pages)
    if name=='con-contrasena-editable':
        assert copy.attachments['nota.txt'][0]==b'Adjunto sintetico local'
        assert copy.get_destination_page_number(copy.outline[0])==0
style=PdfReader(p/'estilo-original.pdf');ops=style.pages[0].get_contents().operations
assert any(op==b'Tf' and abs(float(args[-1])-24)<.001 for args,op in ops)
assert any(op==b'rg' and all(abs(float(a)-b/255)<.0001 for a,b in zip(args,[0xB4,0x23,0x18])) for args,op in ops)
assert 'Texto original compatible 123' in style.pages[0].extract_text()
assert 'Contenido independiente que debe conservarse.' in style.pages[0].extract_text()
print('PASS pypdf copias sin cifrado ni campos de firma, referencias conservadas; tamaño y color en operadores PDF reales')
layered=PdfReader(p/'orden-imagenes.pdf');page=layered.pages[0];names=[args[0] for args,op in page.get_contents().operations if op==b'Do']
assert len(names)==2 and page['/Resources']['/XObject'][names[-1]].get_object().get_data()[:3]==bytes([255,0,0])
print('PASS pypdf orden de imágenes persistido: la imagen roja se pinta después de la azul')
