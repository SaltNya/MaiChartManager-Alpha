import { ref } from 'vue';
import api from '@/client/api';
import type { SinmaiAlphaStatus } from '@/client/apiGen';

export const sinmaiAlpha = ref<SinmaiAlphaStatus>();
export const showAlphaNotice = ref(false);
const notifiedPackages = new Set<string>();
let statusRevision = 0;
export const updateSinmaiAlpha = async () => {
  const revision = ++statusRevision;
  // 切换 Package 或检测失败时，不沿用上一个目录的可用状态。
  sinmaiAlpha.value = undefined;
  showAlphaNotice.value = false;
  const status = (await api.GetSinmaiAlphaStatus()).data;
  if (revision !== statusRevision) return;
  sinmaiAlpha.value = status;
  const key = (status.packagePath ?? '').toLowerCase();
  if (status.installed && !status.suppressNotice && !notifiedPackages.has(key)) {
    notifiedPackages.add(key);
    showAlphaNotice.value = true;
  }
};
export const setAlphaNoticePreference = async (suppressNotice: boolean) => {
  await api.SetSinmaiAlphaPreferences({ suppressNotice });
  if (sinmaiAlpha.value) sinmaiAlpha.value.suppressNotice = suppressNotice;
};
