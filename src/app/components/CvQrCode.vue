<script setup lang="ts">
import QRCode from 'qrcode'

const { t } = useI18n()

// Ref for the QR code data URL
const qrCodeDataUrl = ref<string>('')

// Generate QR code when component is mounted
onMounted(async () => {
  if (typeof window !== 'undefined') {
    try {
      // Get current URL without hash
      const url = new URL(window.location.pathname + window.location.search, window.location.origin).toString()
      
      // Generate QR code as data URL
      qrCodeDataUrl.value = await QRCode.toDataURL(url, {
        width: 200,
        margin: 2,
        color: {
          dark: '#000000',
          light: '#FFFFFF'
        }
      })
    } catch (error) {
      console.error('Failed to generate QR code:', error)
    }
  }
})
</script>

<template>
  <div class="space-y-4">
    <h2 class="text-2xl font-bold text-gray-900 dark:text-white print:text-black mb-4">
      {{ t('qrcode.title') }}
    </h2>
    
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
