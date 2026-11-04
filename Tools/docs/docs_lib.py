"""Utilidades para generar los entregables a partir de una copia limpia de PORTADA.docx.

La portada (sección 1) no se modifica; el cuerpo se escribe en la sección 2 de la plantilla,
que ya inicia en página nueva y hereda el encabezado y la numeración de páginas.
"""
import re
import shutil
from copy import deepcopy
from pathlib import Path

import docx
from docx.enum.section import WD_SECTION
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor

TEMPLATE = Path(r"C:\Users\Diego\Documents\Marista\PORTADA.docx")
OUT_DIR = Path(r"C:\Users\Diego\ejercicios\Entregables")
CAPTURES = Path(r"C:\Users\Diego\ejercicios\Tools\captures")
RENDERS = Path(r"C:\Users\Diego\ejercicios\Tools\renders")
FIGS = Path(r"C:\Users\Diego\ejercicios\Tools\figs")

ACCENT = RGBColor(0x0B, 0x3D, 0x45)
FONT = "Times New Roman"
CODE_FONT = "Consolas"


class Report:
    def __init__(self, filename: str, title: str, subtitle: str = ""):
        OUT_DIR.mkdir(parents=True, exist_ok=True)
        self.path = OUT_DIR / filename
        shutil.copyfile(TEMPLATE, self.path)
        self.doc = docx.Document(self.path)
        self.fig = 0
        self.tab = 0
        self._prepare_body()
        self._styles()
        self._bullet_num = self._make_numbering(bullet=True)
        self.title(title, subtitle)

    # ---------- preparación ----------
    def _prepare_body(self):
        body_sec = self.doc.sections[-1]
        body_sec.left_margin = Cm(2.5)
        body_sec.right_margin = Cm(2.5)
        body_sec.bottom_margin = Cm(2.2)
        # Quitar los párrafos vacíos que la plantilla deja después del salto de sección.
        body = self.doc.element.body
        children = list(body)
        last_sect_p = max(i for i, el in enumerate(children) if el.tag == qn("w:p") and el.find(".//" + qn("w:sectPr")) is not None)
        for el in children[last_sect_p + 1:]:
            if el.tag == qn("w:p") and not "".join(el.itertext()).strip():
                body.remove(el)

    def _styles(self):
        st = self.doc.styles
        normal = st["Normal"]
        normal.font.name = FONT
        normal.font.size = Pt(12)
        normal.element.rPr.rFonts.set(qn("w:eastAsia"), FONT)

        def heading(name, size, level, before, after, color=ACCENT):
            s = st[name] if name in [x.name for x in st] else st.add_style(name, WD_STYLE_TYPE.PARAGRAPH)
            s.base_style = normal
            s.font.name = FONT
            s.font.size = Pt(size)
            s.font.bold = True
            s.font.color.rgb = color
            pf = s.paragraph_format
            pf.space_before = Pt(before)
            pf.space_after = Pt(after)
            pf.keep_with_next = True
            ppr = s.element.get_or_add_pPr()
            ol = OxmlElement("w:outlineLvl")
            ol.set(qn("w:val"), str(level))
            ppr.append(ol)
            return s

        heading("Heading 1", 15, 0, 18, 8)
        heading("Heading 2", 13, 1, 12, 6)
        heading("Heading 3", 12, 2, 10, 4, RGBColor(0x22, 0x22, 0x22))

        body = st.add_style("Cuerpo", WD_STYLE_TYPE.PARAGRAPH)
        body.base_style = normal
        pf = body.paragraph_format
        pf.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
        pf.line_spacing = 1.35
        pf.space_after = Pt(8)
        pf.first_line_indent = Cm(0)

        cap = st.add_style("Leyenda", WD_STYLE_TYPE.PARAGRAPH)
        cap.base_style = normal
        cap.font.size = Pt(10)
        cap.font.italic = True
        cap.font.color.rgb = RGBColor(0x44, 0x44, 0x44)
        cap.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
        cap.paragraph_format.space_after = Pt(12)

        code = st.add_style("Codigo", WD_STYLE_TYPE.PARAGRAPH)
        code.base_style = normal
        code.font.name = CODE_FONT
        code.font.size = Pt(8.5)
        code.paragraph_format.space_after = Pt(0)
        code.paragraph_format.line_spacing = 1.0

    def _make_numbering(self, bullet=True):
        numbering = self.doc.part.numbering_part.element
        abstract_ids = [int(a.get(qn("w:abstractNumId"))) for a in numbering.findall(qn("w:abstractNum"))]
        num_ids = [int(n.get(qn("w:numId"))) for n in numbering.findall(qn("w:num"))]
        aid = max(abstract_ids + [0]) + 1
        nid = max(num_ids + [0]) + 1
        a = OxmlElement("w:abstractNum")
        a.set(qn("w:abstractNumId"), str(aid))
        mlt = OxmlElement("w:multiLevelType"); mlt.set(qn("w:val"), "hybridMultilevel"); a.append(mlt)
        for lvl in range(2):
            l = OxmlElement("w:lvl"); l.set(qn("w:ilvl"), str(lvl))
            start = OxmlElement("w:start"); start.set(qn("w:val"), "1"); l.append(start)
            fmt = OxmlElement("w:numFmt"); fmt.set(qn("w:val"), "bullet" if bullet else "decimal"); l.append(fmt)
            txt = OxmlElement("w:lvlText"); txt.set(qn("w:val"), ("•" if lvl == 0 else "–") if bullet else f"%{lvl + 1}."); l.append(txt)
            jc = OxmlElement("w:lvlJc"); jc.set(qn("w:val"), "left"); l.append(jc)
            ppr = OxmlElement("w:pPr"); ind = OxmlElement("w:ind")
            ind.set(qn("w:left"), str(720 + 360 * lvl)); ind.set(qn("w:hanging"), "360"); ppr.append(ind); l.append(ppr)
            if bullet:
                rpr = OxmlElement("w:rPr"); rf = OxmlElement("w:rFonts")
                rf.set(qn("w:ascii"), "Arial"); rf.set(qn("w:hAnsi"), "Arial"); rpr.append(rf); l.append(rpr)
            a.append(l)
        first_num = numbering.find(qn("w:num"))
        if first_num is not None:
            first_num.addprevious(a)
        else:
            numbering.append(a)
        n = OxmlElement("w:num"); n.set(qn("w:numId"), str(nid))
        ref = OxmlElement("w:abstractNumId"); ref.set(qn("w:val"), str(aid)); n.append(ref)
        numbering.append(n)
        return nid

    # ---------- bloques ----------
    def title(self, text, subtitle=""):
        p = self.doc.add_paragraph(style="Normal")
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_after = Pt(4)
        r = p.add_run(text); r.bold = True; r.font.size = Pt(18); r.font.color.rgb = ACCENT
        if subtitle:
            p2 = self.doc.add_paragraph(style="Normal")
            p2.alignment = WD_ALIGN_PARAGRAPH.CENTER
            p2.paragraph_format.space_after = Pt(4)
            r2 = p2.add_run(subtitle); r2.italic = True; r2.font.size = Pt(12)
        rule = self.doc.add_paragraph(style="Normal")
        pbdr = OxmlElement("w:pBdr"); bottom = OxmlElement("w:bottom")
        for k, v in (("val", "single"), ("sz", "8"), ("space", "1"), ("color", "0B3D45")):
            bottom.set(qn("w:" + k), v)
        pbdr.append(bottom); rule._p.get_or_add_pPr().append(pbdr)
        rule.paragraph_format.space_after = Pt(12)

    def h1(self, text): self.doc.add_paragraph(text, style="Heading 1")
    def h2(self, text): self.doc.add_paragraph(text, style="Heading 2")
    def h3(self, text): self.doc.add_paragraph(text, style="Heading 3")

    def p(self, text, style="Cuerpo", align=None):
        par = self.doc.add_paragraph(style=style)
        _rich(par, text)
        if align == "center":
            par.alignment = WD_ALIGN_PARAGRAPH.CENTER
        return par

    def bullets(self, items, level=0):
        for it in items:
            par = self.doc.add_paragraph(style="Cuerpo")
            par.paragraph_format.space_after = Pt(3)
            numpr = OxmlElement("w:numPr")
            il = OxmlElement("w:ilvl"); il.set(qn("w:val"), str(level)); numpr.append(il)
            ni = OxmlElement("w:numId"); ni.set(qn("w:val"), str(self._bullet_num)); numpr.append(ni)
            par._p.get_or_add_pPr().append(numpr)
            _rich(par, it)
        self.doc.paragraphs[-1].paragraph_format.space_after = Pt(8)

    def numbered(self, items):
        for i, it in enumerate(items, 1):
            par = self.doc.add_paragraph(style="Cuerpo")
            par.paragraph_format.left_indent = Cm(1.0)
            par.paragraph_format.first_line_indent = Cm(-0.6)
            par.paragraph_format.space_after = Pt(3)
            _rich(par, f"{i}.\u00a0 " + it)

    def figure(self, path, caption, width_cm=15.5):
        path = Path(path)
        par = self.doc.add_paragraph(style="Normal")
        par.alignment = WD_ALIGN_PARAGRAPH.CENTER
        par.paragraph_format.keep_with_next = True
        par.paragraph_format.space_before = Pt(6)
        par.add_run().add_picture(str(path), width=Cm(width_cm))
        self.fig += 1
        self.doc.add_paragraph(f"Figura {self.fig}. {caption}", style="Leyenda")
        return self.fig

    def figures_row(self, items, width_cm=7.6):
        """Varias imágenes lado a lado en una tabla sin bordes. items = [(ruta, subtítulo)]."""
        t = self.doc.add_table(rows=2, cols=len(items))
        t.alignment = WD_TABLE_ALIGNMENT.CENTER
        for i, (path, sub) in enumerate(items):
            c = t.cell(0, i); c.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
            c.paragraphs[0].add_run().add_picture(str(path), width=Cm(width_cm))
            c2 = t.cell(1, i); c2.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
            r = c2.paragraphs[0].add_run(sub); r.font.size = Pt(10); r.bold = True

    def caption(self, text):
        self.fig += 1
        self.doc.add_paragraph(f"Figura {self.fig}. {text}", style="Leyenda")
        return self.fig

    def table(self, header, rows, widths_cm=None, caption=None, font_pt=10):
        if caption:
            self.tab += 1
            cp = self.doc.add_paragraph(f"Tabla {self.tab}. {caption}", style="Leyenda")
            cp.paragraph_format.keep_with_next = True
            cp.paragraph_format.space_after = Pt(4)
        t = self.doc.add_table(rows=1 + len(rows), cols=len(header))
        t.alignment = WD_TABLE_ALIGNMENT.CENTER
        _borders(t)
        for j, h in enumerate(header):
            cell = t.cell(0, j)
            _shade(cell, "0B3D45")
            par = cell.paragraphs[0]
            run = par.add_run(h); run.bold = True; run.font.size = Pt(font_pt); run.font.color.rgb = RGBColor(255, 255, 255)
        for i, row in enumerate(rows, 1):
            for j, val in enumerate(row):
                cell = t.cell(i, j)
                if i % 2 == 0:
                    _shade(cell, "EEF4F5")
                par = cell.paragraphs[0]
                _rich(par, str(val), size=font_pt)
        if widths_cm:
            for row in t.rows:
                for j, w in enumerate(widths_cm):
                    row.cells[j].width = Cm(w)
        # Repetir encabezado si la tabla cruza páginas.
        trpr = t.rows[0]._tr.get_or_add_trPr()
        th = OxmlElement("w:tblHeader"); th.set(qn("w:val"), "true"); trpr.append(th)
        spacer = self.doc.add_paragraph(style="Normal")
        spacer.paragraph_format.space_after = Pt(6)
        return t

    def code(self, text, caption=None):
        if caption:
            cp = self.doc.add_paragraph(caption, style="Leyenda")
            cp.alignment = WD_ALIGN_PARAGRAPH.LEFT
            cp.paragraph_format.keep_with_next = True
            cp.paragraph_format.space_after = Pt(2)
        t = self.doc.add_table(rows=1, cols=1)
        _borders(t, color="BBCCCF")
        cell = t.cell(0, 0)
        _shade(cell, "F4F7F8")
        lines = text.rstrip("\n").split("\n")
        first = True
        for line in lines:
            par = cell.paragraphs[0] if first else cell.add_paragraph()
            par.style = self.doc.styles["Codigo"]
            run = par.add_run(line if line else " ")
            run.font.name = CODE_FONT
            run._element.rPr.rFonts.set(qn("w:hAnsi"), CODE_FONT)
            first = False
        spacer = self.doc.add_paragraph(style="Normal")
        spacer.paragraph_format.space_after = Pt(6)

    def page_break(self):
        self.doc.add_paragraph(style="Normal").add_run().add_break(WD_BREAK.PAGE)

    def note(self, text):
        t = self.doc.add_table(rows=1, cols=1)
        _borders(t, color="2EC4B6")
        c = t.cell(0, 0)
        _shade(c, "E8F7F5")
        _rich(c.paragraphs[0], text, size=10.5)
        self.doc.add_paragraph(style="Normal").paragraph_format.space_after = Pt(4)

    def references(self, refs):
        self.h1("Referencias")
        for r in refs:
            par = self.doc.add_paragraph(style="Cuerpo")
            par.paragraph_format.left_indent = Cm(1.0)
            par.paragraph_format.first_line_indent = Cm(-1.0)
            par.paragraph_format.space_after = Pt(4)
            _rich(par, r, size=11)

    def _fix_total_pages(self):
        """En la plantilla el total de "Página X de 21" es texto fijo: se sustituye por un campo NUMPAGES."""
        seen = set()
        for sec in self.doc.sections:
            footer = sec.footer._element
            if id(footer) in seen:
                continue
            seen.add(id(footer))
            for t in list(footer.iter(qn("w:t"))):
                if t.text != "21":
                    continue
                run = t.getparent()
                rpr = run.find(qn("w:rPr"))
                def make(kind, text=None):
                    r = OxmlElement("w:r")
                    if rpr is not None:
                        r.append(deepcopy(rpr))
                    if kind in ("begin", "separate", "end"):
                        fc = OxmlElement("w:fldChar"); fc.set(qn("w:fldCharType"), kind); r.append(fc)
                    elif kind == "instr":
                        it = OxmlElement("w:instrText"); it.set(qn("xml:space"), "preserve"); it.text = " NUMPAGES "; r.append(it)
                    else:
                        tt = OxmlElement("w:t"); tt.text = text; r.append(tt)
                    return r
                for new in (make("begin"), make("instr"), make("separate"), make("text", "1"), make("end")):
                    run.addprevious(new)
                run.getparent().remove(run)

    def save(self):
        self._fix_total_pages()
        self.doc.save(self.path)
        return self.path


# ---------- helpers de formato ----------
_TOKEN = re.compile(r"(\*\*.+?\*\*|\*.+?\*|`.+?`)")


def _rich(par, text, size=None):
    """Soporta **negrita**, *cursiva* y `código` en línea."""
    for part in _TOKEN.split(text):
        if not part:
            continue
        if part.startswith("**") and part.endswith("**"):
            r = par.add_run(part[2:-2]); r.bold = True
        elif part.startswith("`") and part.endswith("`"):
            r = par.add_run(part[1:-1]); r.font.name = CODE_FONT
            r._element.get_or_add_rPr().get_or_add_rFonts().set(qn("w:hAnsi"), CODE_FONT)
            r.font.size = Pt((size or 12) - 1.5)
        elif part.startswith("*") and part.endswith("*") and len(part) > 2:
            r = par.add_run(part[1:-1]); r.italic = True
        else:
            r = par.add_run(part)
        if size and not (part.startswith("`")):
            r.font.size = Pt(size)


def _shade(cell, hex_fill):
    tcpr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear"); shd.set(qn("w:color"), "auto"); shd.set(qn("w:fill"), hex_fill)
    tcpr.append(shd)


def _borders(table, color="9FB4B8"):
    tblpr = table._tbl.tblPr
    b = OxmlElement("w:tblBorders")
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        e = OxmlElement(f"w:{edge}")
        e.set(qn("w:val"), "single"); e.set(qn("w:sz"), "4"); e.set(qn("w:space"), "0"); e.set(qn("w:color"), color)
        b.append(e)
    tblpr.append(b)
    mar = OxmlElement("w:tblCellMar")
    for side, v in (("top", "50"), ("bottom", "50"), ("left", "100"), ("right", "100")):
        e = OxmlElement(f"w:{side}"); e.set(qn("w:w"), v); e.set(qn("w:type"), "dxa"); mar.append(e)
    tblpr.append(mar)
