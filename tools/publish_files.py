# 打印多文件发布要用的 files 列表（Artifact 发布：file_path=dist/pub_index.html，root=仓库根目录）
import os, json
R = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
fs = ['js/' + f for f in sorted(os.listdir(R + '/js'))] + ['assets/' + f for f in sorted(os.listdir(R + '/assets'))]
print(json.dumps([{'path': f} for f in fs], separators=(',', ':')))
