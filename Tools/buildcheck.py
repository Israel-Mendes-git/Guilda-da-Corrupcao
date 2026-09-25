"""Monta o response file do Roslyn e compila o Assembly-CSharp fora do Editor.

Receita da memória `validar-compilacao-sem-abrir-unity`, num script para não
depender de sed/tr com barras invertidas no Git Bash do Windows.
"""
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EDITOR = Path(r"C:/Program Files/Unity/Hub/Editor/2022.3.62f3")
CSPROJ = ROOT / "Assembly-CSharp.csproj"

texto = CSPROJ.read_text(encoding="utf-8-sig")

refs = [h.replace("\\", "/") for h in re.findall(r"<HintPath>([^<]+)</HintPath>", texto)]

for proj in re.findall(r'<ProjectReference Include="([^"]+)"', texto):
    nome = Path(proj.replace("\\", "/")).stem
    refs.append(str(ROOT / "Library" / "ScriptAssemblies" / f"{nome}.dll").replace("\\", "/"))

fontes = {c.replace("\\", "/") for c in re.findall(r'<Compile Include="([^"]+)"', texto)}

# Os .cs criados depois do último refresh do Editor ainda não estão no csproj.
for cs in (ROOT / "Assets" / "Scripts").rglob("*.cs"):
    fontes.add(str(cs.relative_to(ROOT)).replace("\\", "/"))

define = re.search(r"<DefineConstants>([^<]*)</DefineConstants>", texto)

linhas = [
    "-target:library",
    "-nostdlib+",
    "-langversion:9.0",
    f'-out:"{(ROOT / "AssemblyCheck.dll").as_posix()}"',
]
if define:
    linhas.append(f"-define:{define.group(1)}")

linhas += [f'-r:"{r}"' for r in refs]
linhas += [f'"{f}"' for f in sorted(fontes)]

rsp = ROOT / "Temp" / "buildcheck.rsp"
rsp.parent.mkdir(exist_ok=True)
rsp.write_text("\n".join(linhas), encoding="utf-8")

cmd = [
    str(EDITOR / "Editor" / "Data" / "NetCoreRuntime" / "dotnet.exe"),
    str(EDITOR / "Editor" / "Data" / "DotNetSdkRoslyn" / "csc.dll"),
    f"@{rsp.as_posix()}",
]

saida = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
linhas_saida = [l for l in (saida.stdout + saida.stderr).splitlines() if l.strip()]

erros = [l for l in linhas_saida if ": error " in l]
avisos = [l for l in linhas_saida if ": warning " in l]

for l in erros[:40]:
    print(l)

print(f"\nerros: {len(erros)} | avisos: {len(avisos)} | exit: {saida.returncode}")
sys.exit(1 if erros else 0)
