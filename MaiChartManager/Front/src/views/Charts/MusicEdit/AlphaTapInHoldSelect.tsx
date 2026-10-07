import { t } from '@/locales';
import { defineComponent, ref, watch } from 'vue';
import { Select } from '@munet/ui';
import api from '@/client/api';
import { selectedADir } from '@/store/refs';
import { sinmaiAlpha } from '@/store/sinmaiAlpha';

export default defineComponent({
  props: { songId: { type: Number, required: true }, level: { type: Number, required: true } },
  setup(props) {
    const value = ref('default');
    const busy = ref(false);
    const error = ref('');
    let revision = 0;
    watch(() => [sinmaiAlpha.value?.packagePath, selectedADir.value, props.songId, props.level], async () => {
      const token = ++revision;
      busy.value = true; error.value = ''; value.value = 'default';
      try {
        const response = await api.GetAlphaTapInHold(selectedADir.value, props.songId, props.level);
        if (token === revision) value.value = response.data.allowTapInHold == null ? 'default' : response.data.allowTapInHold ? 'on' : 'off';
      } catch { if (token === revision) error.value = t('alpha.tapReadFailed'); }
      finally { if (token === revision) busy.value = false; }
    }, { immediate: true });
    const change = async (next: string) => {
      const token = revision;
      busy.value = true; error.value = '';
      try {
        await api.SetAlphaTapInHold(selectedADir.value, props.songId, props.level, { allowTapInHold: next === 'default' ? null : next === 'on' });
        if (token === revision) value.value = next;
      } catch { if (token === revision) error.value = t('alpha.saveFailed'); }
      finally { if (token === revision) busy.value = false; }
    };
    return () => <div class="flex flex-wrap items-center gap-2">
      <span class="text-sm">{t('alpha.tapInHold')}</span>
      <Select class="min-w-24" aria-label={t('alpha.tapInHold')} title={t('alpha.tapInHoldHint')}
        options={[{ value: 'default', label: t('alpha.default') }, { value: 'on', label: t('alpha.allow') }, { value: 'off', label: t('alpha.disallow') }]}
        value={value.value} disabled={busy.value} onChange={change}/>
      {error.value && <span class="text-xs" role="alert">{error.value}</span>}
    </div>;
  },
});
