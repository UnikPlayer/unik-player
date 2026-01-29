import { style } from '$lib/stores/stores.js'
import { get } from 'svelte/store';

// Прямой импорт для лучшего HMR
import BackPicture from '$lib/players/BackPicture.svelte';
import BigHead from '$lib/players/BigHead.svelte';
import Generic from '$lib/players/Generic.svelte';
import Separate from '$lib/players/Separate.svelte';

const players = {
  BackPicture,
  BigHead,
  Generic,
  Separate
};

export function getAllPlayers() {
  return Object.entries(players).map(([name, component]) => ({
    component,
    name
  }));
}

export function getPickedPlayer(styleName) {
  if (!styleName || !players[styleName]) return [];
  return [{ component: players[styleName], name: styleName }];
}
