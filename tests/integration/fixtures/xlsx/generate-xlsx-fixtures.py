"""
Generate deterministic .xlsx fixtures for 003-ingest-breadth.
Uses openpyxl (pip install openpyxl). No external binaries needed.

Files generated (all deterministic — no timestamps randomization beyond openpyxl's
minimal metadata; reparsing is stable):
 - sample-3sheet.xlsx           — 3 visible sheets, header + 10 data rows each,
                                 Sheet2!B5 = "refund policy: 30-day full refund with receipt",
                                 Sheet2 cached formula value 42.50 (stored as numeric with formula flag).
 - sample-hidden.xlsx           — 2 visible + 1 hidden sheet (HiddenC contains hidden-token-xyz)
 - sample-overcap.xlsx          — >100,000 visible <c> cells (110k) to trigger 413 cell-cap
 - fake-xlsx-from-docx.xlsx     — valid docx zip renamed .xlsx (PK + [Content_Types].xml + word/document.xml,
                                 no xl/workbook.xml) — must be rejected with 400 content does not match type
 - corrupt.xlsx                 — first 20 bytes of sample-3sheet.xlsx (truncated after PK header) → 400
 - empty-hidden-only.xlsx       — single hidden sheet, no visible sheets → 400 no extractable content
"""

import io
import os
import zipfile
import openpyxl
from openpyxl.styles import numbers

OUT_DIR = os.path.dirname(os.path.abspath(__file__))


def write_sample_3sheet():
    wb = openpyxl.Workbook()
    headers = ["Name", "Dept", "Refund Policy"]
    rows = [[f"User{i}", "Sales", f"row {i}"] for i in range(10)]

    ws1 = wb.active
    ws1.title = "Sheet1"
    ws1.append(headers)
    for r in rows:
        ws1.append(r)

    ws2 = wb.create_sheet("Sheet2")
    ws2.append(headers)
    for r in rows:
        ws2.append(r)
    # Known sentence on B5 (row 5 col 2) — overwrite the Dept cell for that row
    # Row indexing: header row1, data rows 2-11, so B5 is dept of User3
    ws2["B5"] = "refund policy: 30-day full refund with receipt"
    # Cached formula cell: Excel stores <f> + <v>42.50</v>. openpyxl writes value + formula together
    # so the sheet XML has <c><f>SUM(A2:A3)</f><v>42.50</v></c>
    cell = ws2["C12"]
    cell.value = 42.50
    cell.data_type = "n"

    ws3 = wb.create_sheet("Sheet3")
    ws3.append(headers)
    for r in rows:
        ws3.append(r)

    # Write formula separately via low-level to ensure <f> appears
    ws2["C12"] = 42.50
    # Hack: inject formula via openpyxl's formula attribute (openpyxl stores formula in value if string starts with =)
    # Instead we manually set after save via zip patch, but for deterministic simple case keep numeric 42.50
    # and also create a genuine formula cell on C13 that openpyxl will serialize as <f>SUM(...)
    ws2["C13"].value = "=SUM(C2:C11)"
    # openpyxl needs a second write to preserve cached value; we set it then overwrite data_type
    # Actually openpyxl discards cached value when formula string present. So we patch zip after save.
    path = os.path.join(OUT_DIR, "sample-3sheet.xlsx")
    wb.save(path)
    # Patch sheet2 xml to add cached value 42.50 for formula cell C13
    _patch_formula_cached_value(path, "Sheet2", "C13", "42.50")
    print(f"wrote {path}")


def _patch_formula_cached_value(xlsx_path, sheet_title, cell_ref, cached_value):
    """Ensure cell <c r=C13><f>SUM(C2:C11)</f><v>42.50</v></c> in sheet XML."""
    import xml.etree.ElementTree as ET

    # Read workbook to find sheet relationship
    with zipfile.ZipFile(xlsx_path, "r") as zin:
        files = {name: zin.read(name) for name in zin.namelist()}
    # Find sheet file by inspecting workbook.xml rels
    # openpyxl uses xl/worksheets/sheet2.xml for Sheet2 (order). We brute-force all sheet xmls
    for name in list(files.keys()):
        if name.startswith("xl/worksheets/sheet") and name.endswith(".xml"):
            xml = files[name].decode("utf-8")
            # Look for formula cell reference
            if f'r="{cell_ref}"' in xml:
                # Ensure <v>42.50 exists after <f>
                # Simple string replacement if missing <v>
                if (
                    f'<c r="{cell_ref}"' in xml
                    and cached_value
                    not in xml.split(f'r="{cell_ref}"')[1].split("</c>")[0]
                ):
                    # Inject <v> after </f>
                    xml = (
                        xml.replace(f"</f></c>", f"</f><v>{cached_value}</v></c>", 1)
                        if "</f></c>" in xml
                        else xml
                    )
                    # If cell has formula but no <v>, add it
                    if f'r="{cell_ref}"' in xml and f"<v>{cached_value}</v>" not in xml:
                        xml = xml.replace(f'<c r="{cell_ref}"', f'<c r="{cell_ref}"')
                        # fallback: inject before </c> for that cell
                        marker = f'r="{cell_ref}"'
                        idx = xml.find(marker)
                        end = xml.find("</c>", idx)
                        if idx != -1 and end != -1:
                            before = xml[:end]
                            after = xml[end:]
                            if "<v>" not in before[idx : end + 200]:
                                before = before + f"<v>{cached_value}</v>"
                                xml = before + after
                    files[name] = xml.encode("utf-8")
    with zipfile.ZipFile(xlsx_path, "w", zipfile.ZIP_DEFLATED) as zout:
        for name, data in files.items():
            zout.writestr(name, data)


def write_sample_hidden():
    wb = openpyxl.Workbook()
    ws1 = wb.active
    ws1.title = "VisibleA"
    ws1.append(["H1", "H2"])
    ws1.append(["a", "b"])
    ws2 = wb.create_sheet("VisibleB")
    ws2.append(["H1", "H2"])
    ws2.append(["c", "d"])
    ws3 = wb.create_sheet("HiddenC")
    ws3.append(["H1", "H2"])
    ws3.append(["hidden-token-xyz", "secret"])
    ws3.sheet_state = "hidden"
    path = os.path.join(OUT_DIR, "sample-hidden.xlsx")
    wb.save(path)
    print(f"wrote {path}")


def write_sample_overcap():
    wb = openpyxl.Workbook()
    # 20 cols x 5500 rows = 110,000 cells (>100k cap)
    cols = 20
    rows_needed = 5500
    ws = wb.active
    ws.title = "S1"
    for r in range(rows_needed):
        ws.append([f"c{c}_r{r}" for c in range(cols)])
    path = os.path.join(OUT_DIR, "sample-overcap.xlsx")
    wb.save(path)
    print(f"wrote {path} ({cols * rows_needed} cells)")


def write_fake_xlsx_from_docx():
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr(
            "[Content_Types].xml",
            '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main"/></Types>',
        )
        z.writestr(
            "_rels/.rels",
            '<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>',
        )
        z.writestr(
            "word/document.xml",
            "<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>docx content hidden inside xlsx extension</w:t></w:r></w:p></w:body></w:document>",
        )
        z.writestr(
            "word/_rels/document.xml.rels",
            '<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"></Relationships>',
        )
    path = os.path.join(OUT_DIR, "fake-xlsx-from-docx.xlsx")
    open(path, "wb").write(buf.getvalue())
    print(f"wrote {path}")


def write_empty_hidden_only():
    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "HiddenOnly"
    ws.append(["H1", "H2"])
    ws.append(["only-hidden", "data"])
    path = os.path.join(OUT_DIR, "empty-hidden-only.xlsx")
    wb.save(path)
    # Patch to make the only sheet hidden (openpyxl forbids saving hidden-only, so patch zip)
    import zipfile

    with zipfile.ZipFile(path, "r") as zin:
        files = {name: zin.read(name) for name in zin.namelist()}
    wb_xml = files["xl/workbook.xml"].decode("utf-8")
    wb_xml = wb_xml.replace('state="visible"', 'state="hidden"')
    if "state=" not in wb_xml:
        wb_xml = wb_xml.replace("<sheet ", '<sheet state="hidden" ')
    files["xl/workbook.xml"] = wb_xml.encode("utf-8")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as zout:
        for n, d in files.items():
            zout.writestr(n, d)
    print(f"wrote {path} (hidden-only patched)")


def write_corrupt():
    src = os.path.join(OUT_DIR, "sample-3sheet.xlsx")
    dst = os.path.join(OUT_DIR, "corrupt.xlsx")
    data = open(src, "rb").read()
    open(dst, "wb").write(data[:20])
    print(f"wrote {dst} (truncated {len(data)} -> 20 bytes)")


if __name__ == "__main__":
    write_sample_3sheet()
    write_sample_hidden()
    write_sample_overcap()
    write_fake_xlsx_from_docx()
    write_empty_hidden_only()
    write_corrupt()
    print("All fixtures written.")
