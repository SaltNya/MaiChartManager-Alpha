import { t } from '@/locales';
import { computed, defineComponent, ref, watch } from 'vue';
import { Select } from '@munet/ui';
import api from '@/client/api';
import { selectedADir } from '@/store/refs';
import { sinmaiAlpha } from '@/store/sinmaiAlpha';

export default defineComponent({
  props: { songId: { type: Number, required: true }, level: { type: Number, required: true } },
  setup(props) {
    const value = ref('None');
    const themes = ref<{ id?: string; name?: string; color?: string }[]>([]);
    const options = computed(() => {
      const items = [{ id: 'None', name: t('alpha.defaultDifficulty'), color: '#777777' }, ...themes.value];
      if (!items.some(item => item.id === value.value)) items.push({ id: value.value, name: t('alpha.themeMissing', { name: value.value }), color: '#777777' });
      return items.map(item => ({ value: item.id!, label: () => <span class="flex items-center gap-2"><span class="w-3 h-3 rounded-full" style={{ backgroundColor: item.color }}/>{item.name}</span> }));
    });
    const busy = ref(false);
    const error = ref('');
    let revision = 0;
    watch(() => [sinmaiAlpha.value?.packagePath, selectedADir.value, props.songId, props.level], async () => {
      const token = ++revision;
      error.value = ''; busy.value = true;
      try {
        const [result, catalog] = await Promise.all([
          api.GetAlphaDifficulty(selectedADir.value, props.songId, props.level), api.GetAlphaDifficultyThemes(),
        ]);
        if (token === revision) { value.value = result.data.theme ?? 'None'; themes.value = catalog.data.map(item => ({ id: item.id ?? "", name: item.name ?? item.id ?? "", color: item.color ?? "#777777" })).filter(item => item.id); }
      } catch { if (token === revision) error.value = t('alpha.themeReadFailed'); }
      finally { if (token === revision) busy.value = false; }
    }, { immediate: true });
    const change = async (theme: string) => {
      const token = revision;
      busy.value = true; error.value = '';
      try {
        await api.SetAlphaDifficulty(selectedADir.value, props.songId, props.level, { theme });
        if (token === revision) value.value = theme;
      } catch { if (token === revision) error.value = t('alpha.saveFailed'); }
      finally { if (token === revision) busy.value = false; }
    };
    return () => <div class="flex flex-wrap items-center gap-2">
      <Select class="min-w-40" aria-label={t('alpha.difficultyAppearance')} options={options.value} value={value.value} disabled={busy.value} onChange={change}/>
      {!sinmaiAlpha.value?.installed && value.value !== 'None' && <span class="text-xs">{t('alpha.requiresMod')}</span>}
      {error.value && <span class="text-xs" role="alert">{error.value}</span>}
    </div>;
  },
});
