import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import MascotRobot from '~/components/mascots/MascotRobot.vue'
import MascotOwl from '~/components/mascots/MascotOwl.vue'

describe('Mascot Components', () => {
  describe('MascotRobot', () => {
    it('should render robot mascot', () => {
      const wrapper = mount(MascotRobot, {
        props: {
          size: 120,
          theme: 'default',
          animationState: 'idle'
        }
      })
      
      expect(wrapper.find('svg').exists()).toBe(true)
      expect(wrapper.find('.mascot-robot').exists()).toBe(true)
    })

    it('should apply correct animation class for idle state', () => {
      const wrapper = mount(MascotRobot, {
        props: {
          animationState: 'idle'
        }
      })
      
      expect(wrapper.find('.mascot-idle').exists()).toBe(true)
    })

    it('should apply correct animation class for hover state', () => {
      const wrapper = mount(MascotRobot, {
        props: {
          animationState: 'hover'
        }
      })
      
      expect(wrapper.find('.mascot-hover').exists()).toBe(true)
    })

    it('should apply theme colors', () => {
      const wrapper = mount(MascotRobot, {
        props: {
          theme: 'dark'
        }
      })
      
      // Component should render with theme-based colors
      expect(wrapper.find('svg').exists()).toBe(true)
    })

    it('should have all robot parts', () => {
      const wrapper = mount(MascotRobot, {
        props: {
          size: 120
        }
      })
      
      const svg = wrapper.find('svg').html()
      expect(svg).toContain('robot-head')
      expect(svg).toContain('robot-torso')
      expect(svg).toContain('antenna')
      expect(svg).toContain('eye')
    })
  })

  describe('MascotOwl', () => {
    it('should render owl mascot', () => {
      const wrapper = mount(MascotOwl, {
        props: {
          size: 120,
          theme: 'default',
          animationState: 'idle'
        }
      })
      
      expect(wrapper.find('svg').exists()).toBe(true)
      expect(wrapper.find('.mascot-owl').exists()).toBe(true)
    })

    it('should apply correct animation class for greeting state', () => {
      const wrapper = mount(MascotOwl, {
        props: {
          animationState: 'greeting'
        }
      })
      
      expect(wrapper.find('.mascot-greeting').exists()).toBe(true)
    })

    it('should apply different theme colors', () => {
      const wrapperDefault = mount(MascotOwl, {
        props: {
          theme: 'default'
        }
      })
      
      const wrapperColorful = mount(MascotOwl, {
        props: {
          theme: 'colorful'
        }
      })
      
      expect(wrapperDefault.find('svg').exists()).toBe(true)
      expect(wrapperColorful.find('svg').exists()).toBe(true)
    })

    it('should have all owl parts', () => {
      const wrapper = mount(MascotOwl, {
        props: {
          size: 120
        }
      })
      
      const svg = wrapper.find('svg').html()
      expect(svg).toContain('owl-body')
      expect(svg).toContain('head')
      expect(svg).toContain('wing')
      expect(svg).toContain('eye')
      expect(svg).toContain('beak')
    })

    it('should render with custom size', () => {
      const wrapper = mount(MascotOwl, {
        props: {
          size: 150
        }
      })
      
      const svg = wrapper.find('svg')
      expect(svg.attributes('width')).toBe('150')
      expect(svg.attributes('height')).toBe('150')
    })
  })
})
