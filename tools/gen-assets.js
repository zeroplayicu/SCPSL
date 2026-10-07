/**
 * gen-assets.js —— 把 tools/out.txt 里的图标文本转成 C# 常量文件 FactionPlugin/GocAssets.cs
 * 用法：node tools/gen-assets.js [图标宽=32]
 */
const fs = require('fs');
const path = require('path');

const width = parseInt(process.argv[2] || '32', 10);
const txtPath = path.join(__dirname, 'out.txt');
const outPath = path.join(__dirname, '..', 'FactionPlugin', 'GocAssets.cs');

const hint = fs.readFileSync(txtPath, 'utf8').replace(/\r?\n$/, '');
const rows = hint.split('\n');
const height = rows.length;

// 转义成 C# 字符串字面量
function escapeForCSharp(s) {
  return s
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\r?\n/g, '\\n');
}

const cs = [
  '// 本文件由 tools/gen-assets.js 自动生成，请勿手工编辑。',
  '// 重新生成步骤：',
  '//   node tools/img2hint.js <图标.png> ' + width + ' tools/out.txt',
  '//   node tools/gen-assets.js ' + width,
  'namespace FactionPlugin',
  '{',
  '    /// <summary>GOC 阵营图标（彩色文本图像，供 hint 显示）</summary>',
  '    public static class GocAssets',
  '    {',
  '        /// <summary>图标尺寸（用于计算显示字号）</summary>',
  '        public const int IconWidth = ' + width + ';',
  '        public const int IconHeight = ' + height + ';',
  '',
  '        /// <summary>图标彩色文本（' + hint.length + ' 字符）</summary>',
  '        public const string Icon =',
  '            "' + escapeForCSharp(hint) + '";',
  '    }',
  '}',
  ''
].join('\r\n');

fs.writeFileSync(outPath, cs, 'utf8');
console.log('已生成: ' + outPath);
console.log('尺寸: ' + width + '×' + height + ' 像素, 文本 ' + hint.length + ' 字符, 文件 ' + cs.length + ' 字符');
