/**
 * AvatarService — generates avatar URLs for users.
 * Mirrors the server-side IAvatarService for client-side previews.
 * SRP: avatar logic only.
 */

const BASE_URL = 'https://api.dicebear.com/7.x/bottts-neutral/svg';
const COLORS = '5865f2,57f287,fee75c,eb459e,ed4245';

export function getAvatarUrl(userName) {
    if (!userName) return `${BASE_URL}?seed=anonymous&backgroundColor=${COLORS}`;
    const seed = encodeURIComponent(userName.trim().toLowerCase());
    return `${BASE_URL}?seed=${seed}&backgroundColor=${COLORS}`;
}
