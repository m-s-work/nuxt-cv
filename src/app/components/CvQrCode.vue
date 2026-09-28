<script setup lang="ts">
const { t } = useI18n()
const { dataUrl: qrCodeDataUrl } = useCvQrCode()
</script>

<template>
  <div class="flex flex-col items-center gap-3">
    <div v-if="qrCodeDataUrl" class="bg-white p-3 rounded-lg shadow-sm border border-gray-200">
      <img 
        :src="qrCodeDataUrl" 
        :alt="t('qrcode.alt')"
        class="w-40 h-40"
      />
    </div>
    <div v-else class="bg-gray-200 dark:bg-gray-700 p-3 rounded-lg shadow-sm border border-gray-200 w-40 h-40 flex items-center justify-center">
      <span class="text-gray-500 dark:text-gray-400 text-sm">{{ t('qrcode.loading') }}</span>
    </div>
    <p class="text-xs text-center text-gray-600 dark:text-gray-400 print:text-gray-700">
      {{ t('qrcode.description') }}
    </p>
  </div>
</template>

<i18n lang="json">
{
  "en": {
    "qrcode": {
      "title": "QR Code",
      "alt": "QR Code to this CV",
      "description": "Scan to open this CV online",
      "loading": "Loading..."
    }
  },
  "de": {
    "qrcode": {
      "title": "QR-Code",
      "alt": "QR-Code zu diesem Lebenslauf",
      "description": "Scannen, um diesen Lebenslauf online zu öffnen",
      "loading": "Lädt..."
    }
  }
}
</i18n>
