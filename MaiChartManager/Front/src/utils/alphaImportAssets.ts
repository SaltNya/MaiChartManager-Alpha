import { t } from '@/locales';
import type { ImportDirectory } from './importDirectory';

// 两种目录句柄都通过相同接口读取相对路径，并保留上传文件的子目录名。
export async function alphaImportAssets(root: ImportDirectory, names: string[]): Promise<File[]> {
  const result: File[] = [];
  for (const name of names) {
    const parts = name.replaceAll('\\', '/').split('/');
    if (parts.some(part => !part || part === '.' || part === '..' || /[:\x00]/.test(part)))
      throw new Error(t('alpha.skinPathInvalid', { name }));
    let directory = root;
    for (const part of parts.slice(0, -1)) {
      let child: ImportDirectory | undefined;
      for await (const entry of directory.values()) {
        if (entry.kind === 'directory' && entry.name.toLowerCase() === part.toLowerCase()) { child = entry; break; }
      }
      if (!child) throw new Error(t('alpha.skinFolderMissing', { name }));
      directory = child;
    }
    let file: File | undefined;
    for await (const entry of directory.values()) {
      if (entry.kind === 'file' && entry.name.toLowerCase() === parts.at(-1)!.toLowerCase()) { file = await entry.getFile(); break; }
    }
    if (!file) throw new Error(t('alpha.skinMissing', { name }));
    result.push(new File([file], name, { type: file.type, lastModified: file.lastModified }));
  }
  return result;
}
