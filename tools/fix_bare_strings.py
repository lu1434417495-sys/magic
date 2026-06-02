"""Fix bare string values (like 'combo_stack') in params dicts to be properly quoted."""
import os, re, glob

SKILLS_DIR = r"E:\game\magic\data\configs\skills"
fixed = 0

for fpath in glob.glob(os.path.join(SKILLS_DIR, "warrior_*.tres")):
    with open(fpath, "r", encoding="utf-8") as f:
        content = f.read()
    original = content

    # Fix within params = { ... } blocks only
    def fix_params_block(match):
        block = match.group(0)
        # Find bare string values: "key": ValueNotInQuotes,
        # where ValueNotInQuotes is not true/false and not a number
        def replace_bare(m):
            key = m.group(1)
            val = m.group(2)
            if val in ("true", "false"):
                return m.group(0)
            if val.isdigit() or (val.startswith('-') and val[1:].isdigit()):
                return m.group(0)
            # Needs quoting
            return f'"{key}": "{val}"'
        
        block = re.sub(r'"(\w+)":\s+(\w+)(?=[,\s\n\}])', replace_bare, block)
        return block

    content = re.sub(r'params\s*=\s*\{[^}]+\}', fix_params_block, content, flags=re.DOTALL)

    if content != original:
        with open(fpath, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
        fixed += 1

print(f"Fixed {fixed} files")
if fixed == 0:
    print("No bare strings found - all files OK")
