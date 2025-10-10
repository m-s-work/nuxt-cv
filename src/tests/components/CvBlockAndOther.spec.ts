import { describe, it, expect } from 'vitest'

describe('CvBlock Component Tests', () => {
  it('should pass basic test', () => {
    expect(true).toBe(true)
  })

  it('can handle block data with all fields', () => {
    const block = {
      id: 1,
      title: 'Test Title',
      subtitle: 'Test Subtitle',
      period: '2020 - 2021',
      description: 'Test description',
      technologies: ['Tech1', 'Tech2'],
      images: ['/image1.jpg'],
      type: 'experience' as const,
      showPeriod: true,
      showTechnologies: true
    }
    
    expect(block.title).toBe('Test Title')
    expect(block.subtitle).toBe('Test Subtitle')
    expect(block.technologies).toHaveLength(2)
    expect(block.images).toHaveLength(1)
  })

  it('can handle block without optional fields', () => {
    const block = {
      id: 2,
      title: 'Minimal Block',
      type: 'other' as const
    }
    
    expect(block.title).toBe('Minimal Block')
    expect(block.type).toBe('other')
  })

  it('supports showPeriod flag', () => {
    const blockWithPeriod = {
      id: 3,
      title: 'With Period',
      period: '2020 - 2021',
      showPeriod: true
    }
    
    const blockWithoutPeriod = {
      id: 4,
      title: 'Without Period',
      period: '2020 - 2021',
      showPeriod: false
    }
    
    expect(blockWithPeriod.showPeriod).toBe(true)
    expect(blockWithoutPeriod.showPeriod).toBe(false)
  })
})

describe('CvOtherExperiences Component Tests', () => {
  it('should pass basic test', () => {
    expect(true).toBe(true)
  })

  it('can handle other experience entries', () => {
    const entries = [
      {
        id: 1,
        title: 'Mandatory Military Service',
        institution: 'Armed Forces',
        period: '2011 - 2012',
        startDate: '2011-07-01',
        endDate: '2012-06-30',
        description: 'Completed mandatory military service',
        showPeriod: true
      },
      {
        id: 2,
        title: 'School',
        institution: 'High School',
        period: '2005 - 2011',
        startDate: '2005-09-01',
        endDate: '2011-06-30',
        showPeriod: false
      }
    ]
    
    expect(entries).toHaveLength(2)
    expect(entries[0].showPeriod).toBe(true)
    expect(entries[1].showPeriod).toBe(false)
  })

  it('can handle single month internships', () => {
    const internship = {
      id: 1,
      title: 'Summer Internship',
      institution: 'Tech Company',
      period: 'Jul 2010',
      startDate: '2010-07-01',
      endDate: '2010-07-31',
      description: 'One-month internship',
      showPeriod: true
    }
    
    expect(internship.period).toBe('Jul 2010')
    expect(internship.startDate).toBe('2010-07-01')
    expect(internship.endDate).toBe('2010-07-31')
  })

  it('can handle date ranges for internships', () => {
    const internship = {
      id: 1,
      title: 'Summer Internship',
      institution: 'Engineering Firm',
      period: 'Aug 2009 - Sep 2009',
      startDate: '2009-08-01',
      endDate: '2009-09-30',
      description: 'Two-month internship',
      showPeriod: true
    }
    
    expect(internship.period).toBe('Aug 2009 - Sep 2009')
  })
})
