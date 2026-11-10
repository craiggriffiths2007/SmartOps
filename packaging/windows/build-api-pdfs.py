#!/usr/bin/env python3
"""Build SmartOps-branded PDF guides from the canonical Markdown files."""
from pathlib import Path
from xml.sax.saxutils import escape
import re
import mistune
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, Preformatted

ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'SmartOps.4D'/'Documentation'
OUT=ROOT/'packaging'/'generated-api-pdfs'
OUT.mkdir(parents=True,exist_ok=True)
FONTS=Path('C:/Windows/Fonts')
pdfmetrics.registerFont(TTFont('SO',str(FONTS/'segoeui.ttf')))
pdfmetrics.registerFont(TTFont('SO-Bold',str(FONTS/'segoeuib.ttf')))
pdfmetrics.registerFont(TTFont('SO-Code',str(FONTS/'consola.ttf')))
pdfmetrics.registerFontFamily('SO',normal='SO',bold='SO-Bold')
N=colors.HexColor('#101E36'); B=colors.HexColor('#2265DB'); C=colors.HexColor('#37B7D6'); G=colors.HexColor('#F3F7FC')
st={
 'p':ParagraphStyle('p',fontName='SO',fontSize=8.7,leading=14,spaceAfter=8,textColor=N),
 'h':ParagraphStyle('h',fontName='SO-Bold',fontSize=16,leading=22,spaceBefore=18,spaceAfter=9,keepWithNext=True,textColor=N),
 'sub':ParagraphStyle('sub',fontName='SO-Bold',fontSize=11,leading=16,spaceBefore=13,spaceAfter=6,keepWithNext=True,textColor=B),
 'bullet':ParagraphStyle('bullet',fontName='SO',fontSize=8.5,leading=13,leftIndent=13,firstLineIndent=-10,spaceAfter=5,textColor=N),
 'cell':ParagraphStyle('cell',fontName='SO',fontSize=7.1,leading=10.5,textColor=N),
 'head':ParagraphStyle('head',fontName='SO-Bold',fontSize=7.1,leading=10.5,textColor=colors.white),
 'code':ParagraphStyle('code',fontName='SO-Code',fontSize=6.7,leading=10.3,textColor=N)
}
def inline(nodes):
 result=''
 for node in nodes:
  kind=node['type']; raw=node.get('raw','')
  if kind=='text': result+=escape(raw)
  elif kind=='codespan': result+='<font color="#2265DB">'+escape(raw)+'</font>'
  elif kind in ('strong','emphasis'):
   tag='b' if kind=='strong' else 'i'
   result+=f'<{tag}>'+inline(node.get('children',[]))+f'</{tag}>'
  elif kind=='link':result+=inline(node.get('children',[]))
  elif kind in ('softbreak','linebreak'):result+=' '
  else:result+=inline(node.get('children',[]))
 return result
def build(source,filename,title,kicker,subtitle):
 ast=mistune.create_markdown(renderer='ast',plugins=['table'])(source.read_text(encoding='utf-8'))
 story=[PageBreak()]
 def parse(nodes):
  for n in nodes:
   kind=n['type']
   if kind=='heading':
    if n.get('attrs',{}).get('level')==1:continue
    story.append(Paragraph(inline(n['children']),st['h' if n['attrs']['level']==2 else 'sub']))
   elif kind=='paragraph':story.append(Paragraph(inline(n['children']),st['p']))
   elif kind=='block_code':
    raw=n.get('raw','').strip('\n')
    rows=[]
    for line in raw.splitlines():
     if len(line)>104:
      rows.extend([line[i:i+104] for i in range(0,len(line),104)])
     else:rows.append(line)
    block=Table([[Preformatted('\n'.join(rows),st['code'])]],colWidths=[A4[0]-108])
    block.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,-1),G),('BOX',(0,0),(-1,-1),.5,colors.HexColor('#DAE5F3')),('LEFTPADDING',(0,0),(-1,-1),12),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),9),('BOTTOMPADDING',(0,0),(-1,-1),9)]))
    story.extend([block,Spacer(1,9)])
   elif kind=='list':
    for idx,item in enumerate(n.get('children',[]),1):
     for child in item.get('children',[]):
      if child['type'] in ('paragraph','block_text'):
       symbol=f'{idx}.' if n.get('attrs',{}).get('ordered') else '•'
       story.append(Paragraph(escape(symbol)+'  '+inline(child.get('children',[])),st['bullet']))
      elif child['type']=='list':parse([child])
   elif kind=='table':
    allrows=[]
    for part in n.get('children',[]):
     if part['type']=='table_head':allrows.append([inline(c.get('children',[])) for c in part.get('children',[])])
     elif part['type']=='table_body':
      allrows += [[inline(c.get('children',[])) for c in row.get('children',[])] for row in part.get('children',[])]
    if allrows:
     count=len(allrows[0]);w=(A4[0]-108)/count
     data=[[Paragraph(v,st['head' if i==0 else 'cell']) for v in row] for i,row in enumerate(allrows)]
     tab=Table(data,colWidths=[w]*count,repeatRows=1)
     tab.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),N),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,G]),('VALIGN',(0,0),(-1,-1),'TOP'),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7)]))
     story.extend([tab,Spacer(1,10)])
   elif kind=='block_quote':parse(n.get('children',[]))
 parse(ast)
 output=OUT/filename
 doc=SimpleDocTemplate(str(output),pagesize=A4,leftMargin=54,rightMargin=54,topMargin=68,bottomMargin=65,title='SmartOps | '+title,author='SmartOps')
 def draw(canvas,doc):
  canvas.saveState()
  if doc.page==1:
   canvas.setFillColor(N);canvas.rect(0,0,A4[0],A4[1],fill=1,stroke=0)
   canvas.setFillColor(colors.HexColor('#17395D'));canvas.circle(A4[0]+120,A4[1]-200,310,fill=1,stroke=0)
   canvas.setFillColor(C);canvas.rect(54,A4[1]-168,85,3,fill=1,stroke=0)
   canvas.setFont('SO-Bold',23);canvas.setFillColor(colors.white);canvas.drawString(54,A4[1]-92,'SMARTOPS')
   canvas.setFont('SO-Bold',10);canvas.setFillColor(C);canvas.drawString(54,A4[1]-142,kicker.upper())
   yy=A4[1]-240
   for line in title.split('|'):
    canvas.setFillColor(colors.white);canvas.setFont('SO-Bold',34);canvas.drawString(54,yy,line);yy-=49
   canvas.setFillColor(colors.HexColor('#D8E9FB'));canvas.setFont('SO',12)
   for line in subtitle.split('|'):canvas.drawString(54,yy-10,line);yy-=23
   canvas.setFont('SO',9);canvas.setFillColor(C);canvas.drawString(54,92,'DEVELOPER REFERENCE  /  NOVEMBER 2026')
   canvas.setFillColor(colors.HexColor('#D8E9FB'));canvas.drawString(54,71,'SmartOps 4D Integration Kit')
  else:
   canvas.setStrokeColor(colors.HexColor('#D8E4F1'));canvas.line(54,A4[1]-46,A4[0]-54,A4[1]-46);canvas.line(54,43,A4[0]-54,43)
   canvas.setFillColor(N);canvas.setFont('SO-Bold',8);canvas.drawString(54,A4[1]-35,'SMARTOPS')
   canvas.setFont('SO',7.5);canvas.drawRightString(A4[0]-54,A4[1]-35,kicker.upper())
   canvas.drawString(54,29,'SmartOps • Developer Integration Reference • November 2026')
   canvas.drawRightString(A4[0]-54,29,str(doc.page))
  canvas.restoreState()
 doc.build(story,onFirstPage=draw,onLaterPages=draw)
 print(output)
build(SRC/'APP_API_INTEGRATION.md','APP_API_INTEGRATION.pdf','Android Survey|API Integration','Native 4D mobile API','Architecture • endpoints • authentication|Downloads, uploads and troubleshooting')
build(SRC/'WEB_API_INTEGRATION.md','WEB_API_INTEGRATION.pdf','ASP.NET Web|API Integration','Optional web bridge','ASP.NET Core • authentication|Snapshot and commit integration')
