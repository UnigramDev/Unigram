decryptedMessage#204d3878 random_id:long ttl:int message:string media:DecryptedMessageMedia = DecryptedMessage;
decryptedMessageService#73164160 random_id:long action:DecryptedMessageAction = DecryptedMessage;
decryptedMessageMediaVideo#524a415d thumb:bytes thumb_w:int thumb_h:int duration:int mime_type:string w:int h:int size:int key:bytes iv:bytes = DecryptedMessageMedia;
decryptedMessageMediaAudio#57e0a9cb duration:int mime_type:string size:int key:bytes iv:bytes = DecryptedMessageMedia;
decryptedMessageLayer#1be31789 random_bytes:bytes layer:int in_seq_no:int out_seq_no:int message:DecryptedMessage = DecryptedMessageLayer;
decryptedMessageActionResend#511110b0 start_seq_no:int end_seq_no:int = DecryptedMessageAction;

decryptedMessageActionRequestKey#f3c9611b exchange_id:long g_a:bytes = DecryptedMessageAction;
decryptedMessageActionAcceptKey#6fe1735b exchange_id:long g_b:bytes key_fingerprint:long = DecryptedMessageAction;
decryptedMessageActionAbortKey#dd05ec6b exchange_id:long = DecryptedMessageAction;
decryptedMessageActionCommitKey#ec2e0b9b exchange_id:long key_fingerprint:long = DecryptedMessageAction;
decryptedMessageActionNoop#a82fdd63 = DecryptedMessageAction;

documentAttributeImageSize#6c37c15c w:int h:int = DocumentAttribute;
documentAttributeAnimated#11b58939 = DocumentAttribute;
documentAttributeSticker#fb0a5727 = DocumentAttribute;
documentAttributeVideo#5910cccb duration:int w:int h:int = DocumentAttribute;
documentAttributeAudio#51448e5 duration:int = DocumentAttribute;
documentAttributeFilename#15590068 file_name:string = DocumentAttribute;
photoSizeEmpty#e17e23c type:string = PhotoSize;
photoSize#77bfb61b type:string location:FileLocation w:int h:int size:int = PhotoSize;
photoCachedSize#e9a734fa type:string location:FileLocation w:int h:int bytes:bytes = PhotoSize;
fileLocationUnavailable#7c596b46 volume_id:long local_id:int secret:long = FileLocation;
fileLocation#53d69076 dc_id:int volume_id:long local_id:int secret:long = FileLocation;
decryptedMessageMediaExternalDocument#fa95b0dd id:long access_hash:long date:int mime_type:string size:int thumb:PhotoSize dc_id:int attributes:Vector<DocumentAttribute> = DecryptedMessageMedia;

messageEntityUnknown offset:int length:int = MessageEntity;
messageEntityMention offset:int length:int = MessageEntity;
messageEntityHashtag offset:int length:int = MessageEntity;
messageEntityBotCommand offset:int length:int = MessageEntity;
messageEntityUrl offset:int length:int = MessageEntity;
messageEntityEmail offset:int length:int = MessageEntity;
messageEntityBold offset:int length:int = MessageEntity;
messageEntityItalic offset:int length:int = MessageEntity;
messageEntityCode offset:int length:int = MessageEntity;
messageEntityPre offset:int length:int language:string = MessageEntity;
messageEntityTextUrl offset:int length:int url:string = MessageEntity;

decryptedMessage flags:# random_id:long ttl:int message:string media:flags.9?DecryptedMessageMedia entities:flags.7?Vector<MessageEntity> via_bot_name:flags.11?string reply_to_random_id:flags.3?long = DecryptedMessage;

decryptedMessageMediaPhoto thumb:bytes thumb_w:int thumb_h:int w:int h:int size:int key:bytes iv:bytes caption:string = DecryptedMessageMedia;

decryptedMessageMediaVideo thumb:bytes thumb_w:int thumb_h:int duration:int mime_type:string w:int h:int size:int key:bytes iv:bytes caption:string = DecryptedMessageMedia;

decryptedMessageMediaDocument thumb:bytes thumb_w:int thumb_h:int mime_type:string size:int key:bytes iv:bytes attributes:Vector<DocumentAttribute> caption:string = DecryptedMessageMedia;

decryptedMessageMediaVideo thumb:bytes thumb_w:int thumb_h:int duration:int mime_type:string w:int h:int size:int key:bytes iv:bytes caption:string = DecryptedMessageMedia;

documentAttributeSticker alt:string stickerset:InputStickerSet = DocumentAttribute;
documentAttributeAudio flags:# duration:int title:flags.0?string performer:flags.1?string waveform:flags.2?bytes = DocumentAttribute;
inputStickerSetShortName short_name:string = InputStickerSet;
inputStickerSetEmpty = InputStickerSet;

decryptedMessageMediaVenue lat:double long:double title:string address:string provider:string venue_id:string = DecryptedMessageMedia;
decryptedMessageMediaWebPage url:string = DecryptedMessageMedia;

// LAYER 46