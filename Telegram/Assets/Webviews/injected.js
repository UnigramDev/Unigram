document.body.style.overflow = 'hidden';
document.body.style.msContentZooming = 'none';
document.body.style.touchAction = 'none';
document.body.style.msTouchAction = 'none';
document.body.style.msContentZoomLimitMax = '100%';

viewport = document.querySelector("meta[name=viewport]");
viewport.setAttribute('content', 'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=0');
