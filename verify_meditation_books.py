#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import os
import re

OUTPUT_DIR = "docs/design/books/generated/meditation"

files = sorted(os.listdir(OUTPUT_DIR))
print("File count:", len(files))
print("First 3:", files[:3])
print("Last 3:", files[-3:])
expected = [f"book_meditation_{i:04d}.md" for i in range(51, 101)]
print("All expected present:", all(f in files for f in expected))
print("Unexpected files:", [f for f in files if f not in expected])

titles = set()
skills = set()
length_issues = []
for i in range(51, 101):
    path = os.path.join(OUTPUT_DIR, f"book_meditation_{i:04d}.md")
    with open(path, "r", encoding="utf-8") as f:
        text = f.read()
    title_match = re.search(r"^# 《(.+)》", text, re.MULTILINE)
    skill_match = re.search(r"- \*\*关联技能\*\*：`([^`]+)`", text)
    if title_match:
        titles.add(title_match.group(1))
    if skill_match:
        skills.add(skill_match.group(1))

    start = text.find("## 书籍内容\n\n")
    end = text.find("\n\n## 设计意图")
    content = text[start + len("## 书籍内容\n\n"):end]
    ch = len(re.findall(r"[\u4e00-\u9fff]", content))
    if ch < 280 or ch > 330:
        length_issues.append((i, ch))

print("Unique titles:", len(titles))
print("Unique skills:", len(skills))
print("Title/skill uniqueness OK:", len(titles) == 50 and len(skills) == 50)
print("Content length issues:", length_issues)
