<?php
declare(strict_types=1);
final class ApiRequest {
    public static function decode(string $raw,string $contentType): array {
        if(strlen($raw)>24576)throw new ApiError('INVALID_REQUEST',413);
        if(strtolower(trim(explode(';',$contentType)[0]))!=='application/json')throw new ApiError('INVALID_REQUEST',415);
        try{$r=json_decode($raw,true,32,JSON_THROW_ON_ERROR);}catch(JsonException){throw new ApiError('INVALID_REQUEST');}
        if(!is_array($r)||array_is_list($r)||array_diff(array_keys($r),['version','product','appVersion','hardware','key','token']))throw new ApiError('INVALID_REQUEST');
        if(isset($r['key'])&&(!is_string($r['key'])||strlen($r['key'])>128))throw new ApiError('INVALID_REQUEST');
        if(isset($r['hardware'])&&(!is_array($r['hardware'])||array_diff(array_keys($r['hardware']),['version','components','combined'])))throw new ApiError('INVALID_REQUEST');
        return $r;
    }
}
