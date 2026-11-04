"""Synthetic fixtures only. Development dependency: reportlab and pypdf."""
from pathlib import Path
from io import BytesIO
from reportlab.pdfgen.canvas import Canvas
from reportlab.lib.colors import HexColor
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DictionaryObject, NameObject, ArrayObject, NumberObject, ByteStringObject, TextStringObject, DecodedStreamObject
import sys
out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
b=BytesIO(); c=Canvas(b, pagesize=(595.276,841.89))
c.setFillColor(HexColor('#235EBC')); c.rect(0,790,595.276,52,fill=1,stroke=0)
c.setFillColorRGB(1,1,1); c.setFont('Helvetica-Bold',20); c.drawString(44,808,'Editor PDF / documento de prueba')
c.setFillColor(HexColor('#203047')); c.setFont('Helvetica',18); c.drawString(50,735,'Texto original compatible 123')
c.setFont('Helvetica',12); c.drawString(50,700,'Contenido independiente que debe conservarse.')
c.setStrokeColor(HexColor('#A4B3C8')); c.rect(50,530,495,120,fill=0,stroke=1)
c.setFont('Helvetica',12); c.drawString(68,620,'Este marco es parte del documento original.')
c.drawString(68,590,'No se utiliza para ocultar ni sustituir texto.')
c.setFillColor(HexColor('#235EBC')); c.circle(95,430,30,fill=1,stroke=0)
c.setFillColor(HexColor('#203047')); c.drawString(50,70,'Documento sintetico, sin datos personales. Pagina 1.')
c.showPage(); c.setFont('Helvetica',18); c.drawString(50,750,'Segunda pagina conservada');c.setFont('Helvetica',12);c.drawString(50,715,'Extraer, duplicar, girar y reordenar.');c.save()
base=b.getvalue(); (out/'externo.pdf').write_bytes(base)
w=PdfWriter(clone_from=PdfReader(BytesIO(base)));w.add_outline_item('Inicio',0);w.add_attachment('nota.txt',b'Adjunto sintetico local');
from pypdf.annotations import Link
w.add_annotation(0,Link(rect=(50,690,400,720),target_page_index=1))
w.pages[0]['/Annots'][-1].get_object()['/Dest'][0]=w.pages[1].indirect_reference
w.add_metadata({'/Title':'Prueba de estructuras','/Author':'Datos sinteticos'});w.write(out/'estructuras.pdf')
w=PdfWriter(clone_from=PdfReader(BytesIO(base)));w.encrypt('', 'owner-synthetic-only',permissions_flag=4);w.write(out/'protegido.pdf')
w=PdfWriter(clone_from=PdfReader(BytesIO(base)))
sig=DictionaryObject({NameObject('/Type'):NameObject('/Sig'),NameObject('/Filter'):NameObject('/Adobe.PPKLite'),NameObject('/SubFilter'):NameObject('/adbe.pkcs7.detached'),NameObject('/ByteRange'):ArrayObject([NumberObject(0),NumberObject(0),NumberObject(0),NumberObject(0)]),NameObject('/Contents'):ByteStringObject(b'SYNTHETIC-NOT-A-VALID-SIGNATURE')})
field=DictionaryObject({NameObject('/FT'):NameObject('/Sig'),NameObject('/Type'):NameObject('/Annot'),NameObject('/Subtype'):NameObject('/Widget'),NameObject('/Rect'):ArrayObject([NumberObject(0)]*4),NameObject('/V'):w._add_object(sig)})
w._root_object[NameObject('/AcroForm')]=w._add_object(DictionaryObject({NameObject('/Fields'):ArrayObject([w._add_object(field)])}));w.write(out/'firma-sintetica.pdf')
w=PdfWriter(clone_from=PdfReader(BytesIO(base)));w._root_object[NameObject('/MarkInfo')]=DictionaryObject({NameObject('/Marked'):__import__('pypdf').generic.BooleanObject(True)});w.write(out/'etiquetado.pdf')
w=PdfWriter(clone_from=PdfReader(BytesIO(base)))
w.add_named_destination('destino-sintetico',1);w.write(out/'destinos.pdf')
w=PdfWriter(clone_from=PdfReader(BytesIO(base)))
w.add_annotation(0,Link(rect=(50,690,400,720),target_page_index=1))
w.pages[0]['/Annots'][-1].get_object()['/Dest'][0]=w.pages[1].indirect_reference
w.write(out/'enlace-interno.pdf')
w=PdfWriter(clone_from=PdfReader(BytesIO(base)))
w.add_annotation(0,Link(rect=(50,690,400,720),url='https://example.invalid/prueba-local'))
note=DictionaryObject({NameObject('/Type'):NameObject('/Annot'),NameObject('/Subtype'):NameObject('/Text'),NameObject('/Rect'):ArrayObject([NumberObject(n) for n in [450,680,475,705]]),NameObject('/Contents'):TextStringObject('Nota independiente de prueba'),NameObject('/T'):TextStringObject('Autor sintetico'),NameObject('/F'):NumberObject(4)})
w.add_annotation(0,note)
appearance=DecodedStreamObject();appearance.set_data(b'q 0 0.2 0.8 RG 2 w 5 15 m 25 35 l 20 5 l 65 25 l 95 15 l S Q')
appearance.update({NameObject('/Type'):NameObject('/XObject'),NameObject('/Subtype'):NameObject('/Form'),NameObject('/BBox'):ArrayObject([NumberObject(n) for n in [0,0,100,40]]),NameObject('/Resources'):DictionaryObject()})
stamp=DictionaryObject({NameObject('/Type'):NameObject('/Annot'),NameObject('/Subtype'):NameObject('/Stamp'),NameObject('/Rect'):ArrayObject([NumberObject(n) for n in [70,470,170,510]]),NameObject('/Contents'):TextStringObject('Sello visual sintetico, no firma digital'),NameObject('/NM'):TextStringObject('stamp-local-test'),NameObject('/F'):NumberObject(4),NameObject('/AP'):DictionaryObject({NameObject('/N'):w._add_object(appearance)})})
w.add_annotation(0,stamp);w.write(out/'anotaciones.pdf')
print('Fixtures locales generadas')
