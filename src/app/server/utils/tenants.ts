export interface TenantConfig {
  id: string
  name: string
  domain?: string
  experiences: Experience[]
  studies: Study[]
  projects: Project[]
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
  images?: string[]  // URLs to images
}

export interface Study {
  id: number
  institution: string
  degree: string
  period: string
  startDate: string  // ISO date format or year
  endDate: string  // ISO date format or year
  focus: string
  technologies?: string[]
}

export interface Project {
  id: number
  name: string
  type: string
  description: string
  technologies: string[]
  period: string
  startDate: string  // ISO date format or year
  endDate: string | null  // ISO date format or year, null for ongoing
  screenshots?: string[]  // URLs to screenshots
  images?: string[]  // URLs to images
  logos?: string[]  // URLs to logos
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
        technologies: ['Nuxt', 'Vue.js', 'Node.js', 'Docker', 'Kubernetes'],
        images: ['/images/placeholder.svg', '/images/placeholder.svg']
      },
      {
        id: 2,
        company: 'Software Solutions Ltd.',
        position: 'Full Stack Developer',
        period: '2017 - 2020',
        startDate: '2017-03-01',
        endDate: '2019-12-31',
        description: 'Developed enterprise web applications and microservices',
        technologies: ['Vue.js', 'Express', 'PostgreSQL', 'Redis'],
        images: ['/images/placeholder.svg']
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
        focus: 'Software Engineering & Distributed Systems',
        technologies: ['Java', 'Python', 'Distributed Systems', 'Software Architecture', 'Microservices']
      },
      {
        id: 2,
        institution: 'University of Technology',
        degree: 'Bachelor of Science in Computer Science',
        period: '2012 - 2015',
        startDate: '2012-09-01',
        endDate: '2015-06-30',
        focus: 'Computer Science Fundamentals',
        technologies: ['C++', 'Java', 'Algorithms', 'Data Structures', 'Databases']
      }
    ],
    projects: [
      {
        id: 1,
        name: 'E-Commerce Platform',
        type: 'Web Application',
        description: 'Built a scalable e-commerce platform with microservices architecture, handling thousands of transactions daily',
        technologies: ['Vue.js', 'Node.js', 'PostgreSQL', 'Redis', 'Docker'],
        period: '2021 - 2023',
        startDate: '2021-03-01',
        endDate: '2023-06-30',
        screenshots: ['/images/placeholder.svg', '/images/placeholder.svg'],
        logos: ['/images/placeholder-logo.svg']
      },
      {
        id: 2,
        name: 'Mobile Banking App',
        type: 'Mobile Application',
        description: 'Developed a secure mobile banking application with biometric authentication and real-time transaction processing',
        technologies: ['React Native', 'TypeScript', 'Firebase', 'REST API'],
        period: '2019 - 2021',
        startDate: '2019-06-01',
        endDate: '2021-02-28',
        screenshots: ['/images/placeholder-mobile.svg', '/images/placeholder-mobile.svg', '/images/placeholder-mobile.svg']
      },
      {
        id: 3,
        name: 'AI Content Generator',
        type: 'SaaS Platform',
        description: 'Created an AI-powered content generation tool using machine learning models for automated content creation',
        technologies: ['Python', 'TensorFlow', 'FastAPI', 'React', 'AWS'],
        period: '2022 - Present',
        startDate: '2022-01-01',
        endDate: null,
        images: ['/images/placeholder.svg', '/images/placeholder.svg'],
        logos: ['/images/placeholder-logo.svg']
      }
    ]
  }
}

export function getTenant(tenantId?: string): TenantConfig {
  return tenants[tenantId || 'default'] || tenants.default
}
