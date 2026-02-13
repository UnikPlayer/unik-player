const { mkdir } = 'fs/promises';
const path = 'path';
const { env } = 'process';

function makingAppDataDir(){

    createFolderInAppData()

}



async function createFolderInAppData() {
  const appDataPath = env.APPDATA; // Обычно 'C:\\Users\Username\AppData\
  const newFolderPath = path.join(appDataPath, 'UnikPlayer');
  const newSkinFolderPaths = path.join(appDataPath + '/UnikPlayer', 'skins');

  try {
    await mkdir(newFolderPath, { recursive: true });
    await mkdir(newSkinFolderPaths, {recursive:true})
    console.log('Папка в AppData создана!!!');
  } catch (err) {
    console.error('чето не так:', err);
  }
}

module.exports = { makingAppDataDir }
