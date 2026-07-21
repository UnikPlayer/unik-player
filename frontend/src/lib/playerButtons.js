import { style } from '$lib/stores/stores.js'
import { ShowNotification } from '$lib/stores/stores.js'

export async function chooseFunc(name){        
    console.log(name + " выбран")

    style.set(name)

}

export async function copyPlayerStyle(name) {
    ShowNotification.set(true)
    const copyUrl = `http://127.0.0.1:27272/player?${name}`;
    try {
        if (navigator.clipboard) {
            await navigator.clipboard.writeText(copyUrl);
        } else {
            const ta = document.createElement('textarea');
            ta.value = copyUrl;
            ta.style.cssText = 'position:fixed;left:-9999px';
            document.body.appendChild(ta);
            ta.select();
            document.execCommand('copy');
            document.body.removeChild(ta);
        }
        console.log('Скопировано!');
    } catch (err) {
        console.error('Не удалось скопировать: ', err);
    }
}
