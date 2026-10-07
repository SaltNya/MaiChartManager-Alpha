import { computed, defineComponent } from 'vue';
import SinmaiAlphaNotice from '@/components/SinmaiAlphaNotice';
import FeedbackErrorDialog from "@/components/FeedbackErrorDialog";
import NeedPurchaseDialog from "@/components/NeedPurchaseDialog";
import StartupErrorDialog from "@/components/StartupErrorDialog";
import { RouterView } from 'vue-router';

import { modalShowing, selectedThemeName, GlobalElementsContainer as UIGlobalElementsContainer, UIThemes } from '@munet/ui';
import styles from './App.module.sass';
import { useWindowSize } from '@vueuse/core';

export default defineComponent({
  setup() {
    const { width, height } = useWindowSize();

    return () => (
      <div
        class={[styles.contentRoot, modalShowing.value && styles.modalOpen]} style={{ '--screen-width': `${width.value}`, '--screen-height': `${height.value}` }}
      >
        <UIGlobalElementsContainer />
        <RouterView />
        <FeedbackErrorDialog />
        <NeedPurchaseDialog />
        <StartupErrorDialog />
        <SinmaiAlphaNotice />
      </div>
    );
  },
});
