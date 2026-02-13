const { detect } = require('tinyld');
const wanakana = require('wanakana');
const { toRomaji } = wanakana;

function characterCheck(activeSesion){

    function convertParts(parts) {
      const converted = parts.map((part) => {
        if (detect(part) === 'ja') {
          return toRomaji(part);
        }
        return part;
      });
  
      return converted.join(' ');
    }

  if(activeSesion.media.title !== undefined ){
    let titleParts = activeSesion.media.title.split(' ');
    activeSesion.media.title = convertParts(titleParts);
    
    //console.log(activeSesion.media.title, '=> title after conversion');

  }

  if(activeSesion.media.artist !== undefined){
    let artistParts = activeSesion.media.artist.split(' ');
    activeSesion.media.artist = convertParts(artistParts);

    //console.log(activeSesion.media.artist, '=> artist after conversion')

  }

  return activeSesion



/*


    
    let titleLang = detect(activeSesion.media.title)
    let artistLang = detect(activeSesion.media.artist)
    
    //console.log(titleLang)
    switch(titleLang){
      case 'ja':
        activeSesion.media.title = wanakana.toRomaji(activeSesion.media.title)
      break;

      case 'ko':
        activeSesion.media.title = wanakana.toRomaji(activeSesion.media.title)
      break;

      case 'zh':
        activeSesion.media.title = wanakana.toRomaji(activeSesion.media.title)
      break;

      default:
        return activeSesion
    } 

    switch(artistLang){
      case 'ja':
        activeSesion.media.artist = wanakana.toRomaji(activeSesion.media.artist)
      break;

      case 'ko':
        activeSesion.media.artist = wanakana.toRomaji(activeSesion.media.artist)
      break;

      case 'zh':
        activeSesion.media.artist = wanakana.toRomaji(activeSesion.media.artist)
      break;
      
      default:
        return activeSesion
    } 

    console.log(JSON.stringify(activeSesion.media.title))
    console.log(JSON.stringify(activeSesion.media.artist))
*/
}

module.exports = { characterCheck }