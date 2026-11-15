#!/usr/bin/env python3
"""碰撞图生成：底图颜色分割（路面）∩ 手画走廊，再用手工的 add / sub 多边形修。
用法：python3 tools/mkmask.py <地图名>   输出预览叠图到 /tmp，并把位图写进 js/mask_<地图名>.js
坐标一律是“数据坐标”（和 data.js 里的 roads 同一套）。"""
import sys, re, json, base64, math, cv2, numpy as np
