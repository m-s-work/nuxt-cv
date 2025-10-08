export interface TenantConfig {
  id: string
  name: string
  domain?: string
  experiences: Experience[]
  studies: Study[]
}

export interface Experience {
  id: number
  company: string
  position: string
  period: string
  startDate: string  // ISO date format or year
  endDate: string | null  // ISO date format or year, null for current
  description: string
  technologies: string[]
}

export interface Study {
  id: number
  institution: string
  degree: string
  period: string
  startDate: string  // ISO date format or year
  endDate: string  // ISO date format or year
  focus: string
}

// Multi-tenant configuration
export const tenants: Record<string, TenantConfig> = {
  default: {
    id: 'default',
    name: 'Default CV',
    experiences: [
      {
        id: 1,
        company: 'Tech Company Inc.',
        position: 'Senior Software Architect',
        period: '2020 - Present',
        startDate: '2020-01-01',
        endDate: null,
        description: 'Leading architecture design and implementation for cloud-native applications',
        technologies: ['Nuxt', 'Vue.js', 'Node.js', 'Docker', 'Kubernetes']
      },
      {
        id: 2,
        company: 'Software Solutions Ltd.',
        position: 'Full Stack Developer',
        period: '2017 - 2020',
        startDate: '2017-03-01',
        endDate: '2019-12-31',
        description: 'Developed enterprise web applications and microservices',
        technologies: ['Vue.js', 'Express', 'PostgreSQL', 'Redis']
      }
    ],
    studies: [
      {
        id: 1,
        institution: 'Technical University',
        degree: 'Master of Science in Computer Science',
        period: '2015 - 2017',
        startDate: '2015-09-01',
        endDate: '2017-06-30',
        focus: 'Software Engineering & Distributed Systems'
      },
      {
        id: 2,
        institution: 'University of Technology',
        degree: 'Bachelor of Science in Computer Science',
        period: '2012 - 2015',
        startDate: '2012-09-01',
        endDate: '2015-06-30',
        focus: 'Computer Science Fundamentals'
      }
    ]
  }
}

export function getTenant(tenantId?: string): TenantConfig {
  return tenants[tenantId || 'default'] || tenants.default
}
