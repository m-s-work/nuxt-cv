# GitHub Pages Setup Guide

This guide will help you deploy your Nuxt CV application to GitHub Pages.

## Prerequisites

- A GitHub repository with this code
- GitHub account with repository write access

## Step 1: Enable GitHub Pages

1. Go to your repository on GitHub
2. Click on **Settings** (in the repository menu)
3. Scroll down to the **Pages** section (in the left sidebar under "Code and automation")
4. Under **Build and deployment**:
   - **Source**: Select "GitHub Actions"
   - This allows the workflow to deploy directly

![GitHub Pages Settings](https://docs.github.com/assets/cb-47267/mw-1440/images/help/pages/publishing-source-drop-down.webp)

## Step 2: Push to Main Branch

The deployment workflow is triggered automatically when you push to the `main` branch.

```bash
git push origin main
```

## Step 3: Monitor Deployment

1. Go to the **Actions** tab in your repository
2. You should see a workflow run named "Deploy to GitHub Pages"
3. Click on it to see the progress
4. Wait for both jobs to complete:
   - `build` - Builds the static site
   - `deploy` - Deploys to GitHub Pages

## Step 4: Access Your Site

Once deployed, your site will be available at:

```
https://<username>.github.io/<repository-name>/
```

For example:
- Username: `m-s-work`
- Repository: `nuxt-cv`
- URL: `https://m-s-work.github.io/nuxt-cv/`

## Troubleshooting

### Site Not Loading / 404 Error

**Check the base URL:**

The workflow automatically sets the base URL based on your repository name. If you're still getting 404s:

1. Verify the workflow completed successfully
2. Check that GitHub Pages is enabled in Settings → Pages
3. Ensure the source is set to "GitHub Actions"

### Assets Not Loading

If the page loads but CSS/JS files give 404:

1. Check the `NUXT_APP_BASE_URL` in the deploy workflow
2. Make sure it's set to `/${{ github.event.repository.name }}/`

### Workflow Failing

**Common issues:**

1. **Node modules cache miss**: The workflow will install dependencies, this is normal
2. **Build errors**: Check the logs in the Actions tab
3. **Permission errors**: Ensure GitHub Actions has write permission to deploy

To fix permissions:
- Go to Settings → Actions → General
- Under "Workflow permissions", select "Read and write permissions"
- Save

## Custom Domain (Optional)

To use a custom domain like `cv.example.com`:

1. Go to Settings → Pages
2. Under "Custom domain", enter your domain
3. Add DNS records at your domain provider:
   ```
   CNAME record: cv → <username>.github.io
   ```
4. Wait for DNS to propagate (can take up to 24 hours)
5. Enable "Enforce HTTPS" once DNS is configured

## Manual Deployment (Alternative)

If you prefer not to use GitHub Actions:

1. Build locally:
   ```bash
   cd src
   npm run generate
   ```

2. The output will be in `src/.output/public/`

3. Deploy this folder manually using:
   - GitHub Pages with manual upload
   - Netlify
   - Vercel
   - Any static hosting service

## Updating the Site

Simply push changes to the main branch:

```bash
git add .
git commit -m "Update CV content"
git push origin main
```

The workflow will automatically rebuild and redeploy.

## Testing Before Deploy

To preview your build locally before deploying:

```bash
cd src
npm run generate
npx serve .output/public
```

Then visit `http://localhost:3000` to see the generated static site.

## Environment Variables

For GitHub Pages deployment, environment variables are set in the workflow file (`.github/workflows/deploy.yml`).

To add custom environment variables:

1. Edit `.github/workflows/deploy.yml`
2. Add under the "Build with Nuxt" step:
   ```yaml
   env:
     NUXT_APP_BASE_URL: /${{ github.event.repository.name }}/
     YOUR_VARIABLE: value
   ```

## Support

If you encounter issues:

1. Check the [GitHub Actions logs](../../actions)
2. Review the [GitHub Pages documentation](https://docs.github.com/pages)
3. Open an issue in this repository
