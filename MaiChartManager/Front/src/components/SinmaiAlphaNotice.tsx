import { t } from '@/locales';
import { defineComponent, ref } from 'vue';
import { Button, CheckBox, Modal } from '@munet/ui';
import { setAlphaNoticePreference, showAlphaNotice, sinmaiAlpha } from '@/store/sinmaiAlpha';

export default defineComponent({
  setup() {
    const never = ref(false);
    const busy = ref(false);
    const error = ref('');
    const close = async () => {
      busy.value = true;
      try {
        if (never.value) await setAlphaNoticePreference(true);
        showAlphaNotice.value = false;
      } catch { error.value = t('alpha.saveFailed'); }
      finally { busy.value = false; }
    };
    return () => <Modal title={t('alpha.noticeTitle')} width="min(90vw,34em)" v-model:show={showAlphaNotice.value}>
      {{default: () => <div class="flex flex-col gap-3">
        <p>{t('alpha.noticeDescription', { version: sinmaiAlpha.value?.version ?? '' })}</p>
        {!sinmaiAlpha.value?.assetsReady && <p>{t('alpha.missingAssets')}</p>}
        <CheckBox v-model:value={never.value}>{t('alpha.neverShow')}</CheckBox>
        {error.value && <p role="alert">{error.value}</p>}
      </div>, actions: () => <Button ing={busy.value} onClick={close}>{t('common.close')}</Button>}}
    </Modal>;
  },
});
