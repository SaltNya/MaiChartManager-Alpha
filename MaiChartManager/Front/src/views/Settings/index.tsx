import { defineComponent } from "vue";
import SinmaiAlphaSection from './SinmaiAlphaSection';
import AppearanceSection from "./AppearanceSection";
import GameDirectorySection from "./GameDirectorySection";
import ImportOptionsSection from "./ImportOptionsSection";
import AquaMaiSection from "./AquaMaiSection";

export default defineComponent({
  setup() {
    return () => (
      <div class="p-xy h-100dvh of-y-auto cst">
        <AppearanceSection />
        <GameDirectorySection />
        <ImportOptionsSection />
        <SinmaiAlphaSection />
        <AquaMaiSection />
      </div>
    );
  },
});
