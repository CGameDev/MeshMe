<?php
declare(strict_types=1);
final class ApiError extends RuntimeException {
    public function __construct(public readonly string $stableCode, public readonly int $httpStatus=400) {parent::__construct($stableCode);}
}
final class Keys {
    public const ALPHABET='0123456789ABCDEFGHJKMNPQRSTVWXYZ';
    public static function normalize(string $key): string {return strtoupper(preg_replace('/[\s-]/u','',$key));}
    public static function check(string $key): bool {
        $key=self::normalize($key); if(strlen($key)!==25)return false;$sum=0;
        for($i=0;$i<25;$i++){ $n=strpos(self::ALPHABET,$key[$i]);if($n===false)return false;if($i<24)$sum=($sum*17+$n)%32; }
        return $key[24]===self::ALPHABET[$sum];
    }
    public static function generate(): string {
        $key='';$sum=0;foreach(unpack('C*',random_bytes(24)) as $byte){$n=$byte&31;$key.=self::ALPHABET[$n];$sum=($sum*17+$n)%32;}
        return implode('-',str_split($key.self::ALPHABET[$sum],5));
    }
}
final class Tokens {
    public function __construct(private readonly array $config) {}
    public function sign(array $payload): array {
        $payload['keyId']=$this->config['signing_key_id'];
        $private=openssl_pkey_get_private(file_get_contents($this->config['signing_key_path']));
        if(!$private||openssl_pkey_get_details($private)['bits']<3072||openssl_pkey_get_details($private)['type']!==OPENSSL_KEYTYPE_RSA)throw new RuntimeException('Signing key unavailable');
        $bytes=json_encode($payload,JSON_THROW_ON_ERROR|JSON_UNESCAPED_SLASHES);
        if(!openssl_sign($bytes,$signature,$private,OPENSSL_ALGO_SHA256))throw new RuntimeException('Signing failed');
        return ['payload'=>base64_encode($bytes),'signature'=>base64_encode($signature),'keyId'=>$payload['keyId']];
    }
    public function verify(array $token): array {
        if(!is_string($token['payload']??null)||strlen($token['payload'])>16384||!is_string($token['signature']??null)||strlen($token['signature'])>1024||!is_string($token['keyId']??null))throw new ApiError('INVALID_KEY');
        $path=$this->config['public_keys'][$token['keyId']]??null;if(!$path)throw new ApiError('INVALID_KEY');
        $bytes=base64_decode($token['payload'],true);$sig=base64_decode($token['signature'],true);
        if($bytes===false||$sig===false||openssl_verify($bytes,$sig,file_get_contents($path),OPENSSL_ALGO_SHA256)!==1)throw new ApiError('INVALID_KEY');
        $p=json_decode($bytes,true,32,JSON_THROW_ON_ERROR);
        if(($p['version']??0)!==1||($p['product']??'')!=='meshme360'||($p['keyId']??'')!==$token['keyId']||!in_array($p['type']??'', ['trial','license'],true)||!preg_match('/^[0-9a-f]{32}$/D',$p['id']??'')||($p['issuedAt']??PHP_INT_MAX)>time()+300)throw new ApiError('INVALID_KEY');
        Fingerprint::validate($p['hardware']); return $p;
    }
}
