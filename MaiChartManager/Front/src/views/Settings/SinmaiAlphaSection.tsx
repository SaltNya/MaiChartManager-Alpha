import { t } from '@/locales';
import { defineComponent, ref } from 'vue';
import { Button, CheckBox } from '@munet/ui';
import { sinmaiAlpha, updateSinmaiAlpha, setAlphaNoticePreference } from '@/store/sinmaiAlpha';
export default defineComponent({
  setup() {
    const error = ref('');
    const set = async (enabled: boolean) => {
      try { await setAlphaNoticePreference(!enabled); error.value = ''; }
      catch { error.value = t('alpha.saveFailed'); }
    };
    return () => <div class="mb-6">
      <div class="text-lg font-semibold mb-3 text-[var(--link-color)]">Sinmai-Alpha</div>
      <div class="rounded-xl bg-white/60 p-4 flex flex-col gap-3 border border-gray-200 border-solid">
        <div>{sinmaiAlpha.value?.installed ? t('alpha.installedVersion', { version: sinmaiAlpha.value.version ?? '' }) : t('alpha.notInstalled')}</div>
        <CheckBox value={!sinmaiAlpha.value?.suppressNotice} onChange={()=>set(!!sinmaiAlpha.value?.suppressNotice)}>{t('alpha.showNotice')}</CheckBox>
        <div>{t('alpha.previewOnDemand')}</div>
        <Button onClick={updateSinmaiAlpha}>{t('alpha.detectAgain')}</Button>
        {error.value && <p role="alert">{error.value}</p>}
      </div>
    </div>;
  },
});
