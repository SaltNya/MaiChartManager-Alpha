import { t } from '@/locales';
import { defineComponent, ref, onBeforeUnmount } from 'vue';
import { Button } from '@munet/ui';
import api from '@/client/api';
import { selectedADir } from '@/store/refs';

export default defineComponent({
  props: { songId:{type:Number,required:true}, level:{type:Number,required:true}, side:String },
  setup(props) {
    const busy=ref(false), message=ref(''), session=ref('');
    let generation=0;
    const stop=async () => {
      ++generation;
      const id=session.value; session.value='';
      if(id) await api.StopAlphaPreview(id);
    };
    const play=async () => {
      if(busy.value) return;
      const token=++generation;
      busy.value=true; message.value='';
      try {
        // 直接进入播放器；流速、播放位置等在预览内调整和保存。
        const result=(await api.StartAlphaPreview(selectedADir.value,props.songId,props.level,
          {noteSpeed:7,touchSpeed:7,startTime:0}, {side:props.side})).data;
        if(token!==generation) { if(result.session) await api.StopAlphaPreview(result.session); return; }
        session.value=result.session??''; message.value='';
      } catch(e:any) {
        if(token===generation) message.value=typeof e?.error==='string'?e.error:(e?.error?.detail??e?.message??t('alpha.previewFailed'));
      } finally { busy.value=false; }
    };
    onBeforeUnmount(()=>{ void stop().catch(()=>{}); });
    return () => <span class="inline-flex items-center gap-2">
      <Button onClick={play}>{t('alpha.preview')}</Button>
      {message.value&&<span role="status" class="text-sm">{message.value}</span>}
    </span>;
  },
});
