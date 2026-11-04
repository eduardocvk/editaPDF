"""Convert the project logo to Windows ICO/MSIX sizes; no network access."""
from pathlib import Path
from PIL import Image
root=Path(__file__).resolve().parent.parent
image=Image.open(root/'assets/logo.png').convert('RGBA')
image.save(root/'assets/EditorPDF.ico',sizes=[(n,n) for n in [16,20,24,32,40,48,64,128,256]])
for name,size in [('StoreLogo.png',50),('Square44x44Logo.png',44),('Square150x150Logo.png',150)]:
    image.resize((size,size),Image.Resampling.LANCZOS).save(root/'assets'/name)
print('ICO and MSIX logos generated from assets/logo.png')
