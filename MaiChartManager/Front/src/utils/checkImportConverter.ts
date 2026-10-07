import { t } from '@/locales';
import api from '@/client/api';
import { showTransactionalDialog } from '@munet/ui';

// 检查与实际导入共用本次选择；拒绝 Alpha 时明确使用原版，绝不静默回退。
export async function checkImportConverter(file: File, name: string, isReplacement = false, cancelled = () => false) {
  let check = (await api.ImportChartCheck({ file, isReplacement })).data;
  if (cancelled()) throw { name: 'AbortError' };
  let useAlpha = false;
  if (check.requiresAlphaChoice) {
    useAlpha = await showTransactionalDialog<boolean>(t('alpha.selectConverter'),
      t('alpha.useConverter', { name }),
      [{ text: t('alpha.yes'), action: true }, { text: t('alpha.no'), action: false }]);
    if (cancelled()) throw { name: 'AbortError' };
    check = (await api.ImportChartCheck({ file, isReplacement, useAlpha })).data;
  }
  if (cancelled()) throw { name: 'AbortError' };
  return { check, useAlpha };
}
