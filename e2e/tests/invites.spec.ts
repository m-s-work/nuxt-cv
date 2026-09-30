import { expect, SHARED_URL, test, unique, waitForCv } from './fixtures'

test.describe('invites', () => {
  test('shared host shows the showcase and opens a CV with an invite code', async ({ page, admin }) => {
    const invite = await admin.createInvite('demo', { profile: 'recruiter', label: unique('showcase') })

    await page.goto(`${SHARED_URL}/`)
    await expect(page.locator('h1').first()).toBeVisible()
    await expect(page.getByPlaceholder('Invite code')).toBeVisible()

    // A wrong code is rejected, the right one opens the CV.
    await page.getByPlaceholder('Invite code').fill('not-a-valid-code')
    await page.getByRole('button', { name: 'Open CV' }).click()
    await expect(page.getByRole('alert')).toHaveText('This invite code is not valid.')

    await page.getByPlaceholder('Invite code').fill(invite.code)
    await page.getByRole('button', { name: 'Open CV' }).click()
    await waitForCv(page, 'Max Mustermann')
  })

  test('invite link grants its profile and is removed from the address bar', async ({ page, admin }) => {
    const invite = await admin.createInvite('demo', { profile: 'recruiter', label: unique('recruiter') })

    const cvResponse = page.waitForResponse(r => r.url().includes('/api/cv?'))
    await page.goto(`/?c=${invite.code}`)
    const body = await (await cvResponse).json()

    await waitForCv(page, 'Max Mustermann')
    expect(page.url()).not.toContain(invite.code)
    expect(body.access).toMatchObject({ tenant: 'demo', profile: 'recruiter', viaInvite: true })
    // "recruiter" has the contact grant and shows projects and the photo.
    await expect(page.getByRole('link', { name: 'max.mustermann@example.com' }).first()).toBeVisible()
    await expect(page.locator('#projects-section')).toBeVisible()
    expect(body.cv.profile.photoUrl).toMatch(/^\/api\/assets\//)

    // Assets referenced by the visitor's CV are served, the access cookie keeps working after a reload.
    const photo = await page.request.get(body.cv.profile.photoUrl)
    expect(photo.ok()).toBeTruthy()
    await page.reload()
    await waitForCv(page, 'Max Mustermann')
    await expect(page.getByRole('link', { name: 'max.mustermann@example.com' }).first()).toBeVisible()
  })

  test('tenant without own host is opened via invite on the shared host', async ({ page, admin }) => {
    const invite = await admin.createInvite('bob', { profile: 'full', label: unique('bob') })
    expect(invite.link).toContain(`${SHARED_URL}/?c=`)

    await page.goto(invite.link)
    await waitForCv(page, 'Bob Builder')
    await expect(page.getByRole('link', { name: 'bob@example.com' }).first()).toBeVisible()
  })

  test('revoked invite no longer opens the CV', async ({ browser, admin }) => {
    const invite = await admin.createInvite('bob', { profile: 'anonymous', label: unique('revoke') })
    const revoke = await admin.http.delete(`api/admin/tenants/bob/invites/${invite.id}`)
    expect(revoke.ok()).toBeTruthy()

    const context = await browser.newContext()
    const page = await context.newPage()
    await page.goto(invite.link)
    await expect(page.getByPlaceholder('Invite code')).toBeVisible()
    await expect(page.getByRole('alert')).toHaveText('This invite code is not valid.')
    await context.close()
  })

  test('view-once invite works only in the browser that opened it', async ({ browser, admin }) => {
    const invite = await admin.createInvite('bob', { profile: 'anonymous', label: unique('view-once'), viewOnce: true })

    const first = await browser.newContext()
    const firstPage = await first.newPage()
    await firstPage.goto(invite.link)
    await waitForCv(firstPage, 'Bob Builder')
    // Reloading within the grace window keeps working in this browser.
    await firstPage.reload()
    await waitForCv(firstPage, 'Bob Builder')

    const second = await browser.newContext()
    const secondPage = await second.newPage()
    await secondPage.goto(invite.link)
    await expect(secondPage.getByRole('alert')).toHaveText('This invite code is not valid.')
    await Promise.all([first.close(), second.close()])
  })
})
