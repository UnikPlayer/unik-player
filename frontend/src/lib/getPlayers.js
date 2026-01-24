import { style } from '$lib/stores/stores.js'
import { get } from 'svelte/store';

// Прямой импорт для лучшего HMR
import BackPicture from '$lib/players/BackPicture.svelte';
import BigHead from '$lib/players/BigHead.svelte';
import Generic from '$lib/players/Generic.svelte';
import Separate from '$lib/players/Separate.svelte';
import Square from '$lib/players/Square.svelte';

const players = {
  BackPicture,
  BigHead,
  Generic,
  Separate,
  Square
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
