/**
 * OAuth 2.0 + PKCE 工具 composable
 * 基于 Web Crypto API，无需额外依赖
 */

/**
 * 生成 PKCE Code Verifier（43-128 字符的 URL 安全随机字符串）
 */
export function generateCodeVerifier() {
  const array = new Uint8Array(32)
  crypto.getRandomValues(array)
  return base64UrlEncode(array)
}

/**
 * 计算 PKCE S256 Code Challenge
 * SHA256(verifier) → base64url
 */
export async function generateCodeChallenge(verifier) {
  const encoder = new TextEncoder()
  const data = encoder.encode(verifier)
  const hash = await crypto.subtle.digest('SHA-256', data)
  return base64UrlEncode(new Uint8Array(hash))
}

/**
 * 生成 OAuth State（防 CSRF）
 */
export function generateState() {
  const array = new Uint8Array(16)
  crypto.getRandomValues(array)
  return Array.from(array, b => b.toString(16).padStart(2, '0')).join('')
}

/**
 * 生成 OIDC Nonce
 */
export function generateNonce() {
  return generateState()
}

/**
 * Uint8Array → base64url（无 padding）
 */
function base64UrlEncode(array) {
  let binary = ''
  for (let i = 0; i < array.length; i++) {
    binary += String.fromCharCode(array[i])
  }
  return btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

/**
 * 初始化 OAuth 2.0 授权码流程
 * 生成 PKCE 参数 → 存储到 sessionStorage → 跳转 /authorize
 */
export async function initiateOAuthLogin(issuer = 'http://localhost:5000') {
  const codeVerifier = generateCodeVerifier()
  const codeChallenge = await generateCodeChallenge(codeVerifier)
  const state = generateState()
  const nonce = generateNonce()

  // 存储到 sessionStorage（只对当前标签页有效）
  sessionStorage.setItem('oauth_code_verifier', codeVerifier)
  sessionStorage.setItem('oauth_state', state)
  sessionStorage.setItem('oauth_nonce', nonce)

  const params = new URLSearchParams({
    response_type: 'code',
    client_id: 'vue-spa',
    redirect_uri: window.location.origin + '/callback',
    scope: 'openid profile email',
    state: state,
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    nonce: nonce,
  })

  const authorizeUrl = `${issuer}/authorize?${params.toString()}`
  window.location.href = authorizeUrl
}

/**
 * 处理 OAuth 回调
 * 提取 code → 验证 state → POST /token → 存储令牌
 */
export async function handleOAuthCallback() {
  const urlParams = new URLSearchParams(window.location.search)
  const code = urlParams.get('code')
  const state = urlParams.get('state')
  const error = urlParams.get('error')
  const errorDescription = urlParams.get('error_description')

  if (error) {
    throw new Error(errorDescription || `OAuth 授权失败: ${error}`)
  }

  if (!code) {
    throw new Error('缺少 authorization code')
  }

  // 验证 state
  const storedState = sessionStorage.getItem('oauth_state')
  if (state && storedState && state !== storedState) {
    throw new Error('State 不匹配，可能存在 CSRF 攻击')
  }

  // 获取 PKCE verifier
  const codeVerifier = sessionStorage.getItem('oauth_code_verifier')
  if (!codeVerifier) {
    throw new Error('缺少 PKCE code_verifier')
  }

  // 清理 sessionStorage
  sessionStorage.removeItem('oauth_code_verifier')
  sessionStorage.removeItem('oauth_state')
  sessionStorage.removeItem('oauth_nonce')

  // 交换令牌
  const { exchangeCodeForToken } = await import('../api/auth')
  const response = await exchangeCodeForToken({
    grantType: 'authorization_code',
    code: code,
    codeVerifier: codeVerifier,
    clientId: 'vue-spa',
    redirectUri: window.location.origin + '/callback',
  })

  const { accessToken, refreshToken, idToken, expiresIn } = response.data

  // 存储令牌
  localStorage.setItem('token', accessToken)
  if (refreshToken) localStorage.setItem('refresh_token', refreshToken)
  if (idToken) localStorage.setItem('id_token', idToken)

  // 获取用户信息
  try {
    const { getUserInfo } = await import('../api/auth')
    const userInfo = await getUserInfo()
    localStorage.setItem('user', JSON.stringify({
      username: userInfo.data.preferredUsername || userInfo.data.name,
      email: userInfo.data.email,
      permissions: userInfo.data.permissions || [],
      roleIds: userInfo.data.roleIds || [],
    }))
    localStorage.setItem('permissions', JSON.stringify(userInfo.data.permissions || []))
  } catch {
    // 如果 userinfo 失败，用 id_token 中的信息
    console.warn('获取用户信息失败，使用 id_token')
  }

  return { accessToken, refreshToken }
}
