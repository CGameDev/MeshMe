<?php
declare(strict_types=1);
final class Fingerprint {
    public const WEIGHTS=['systemUuid'=>50,'baseboard'=>25,'bios'=>15,'machineGuid'=>10];
    public static function validate(array $h): void {
        if (($h['version']??null)!==1 || !is_array($h['components']??null) || count($h['components'])===0) throw new ApiError('DEVICE_CHANGED',422);
        foreach($h['components'] as $name=>$hash) if (!isset(self::WEIGHTS[$name]) || !is_string($hash) || !preg_match('/^[0-9a-f]{64}$/D',$hash)) throw new ApiError('DEVICE_CHANGED',422);
        $c=$h['components']; ksort($c,SORT_STRING); $parts=[]; foreach($c as $k=>$v)$parts[]=$k.':'.$v;
        if (!is_string($h['combined']??null) || !hash_equals(hash('sha256',implode("\n",$parts)),$h['combined'])) throw new ApiError('DEVICE_CHANGED',422);
        if (array_sum(array_intersect_key(self::WEIGHTS,$c))<60 || (!isset($c['systemUuid'])&&!isset($c['baseboard']))) throw new ApiError('DEVICE_CHANGED',422);
    }
    public static function matches(array $a,array $b): bool {
        foreach(['systemUuid','baseboard'] as $k) if(isset($a['components'][$k],$b['components'][$k])&&!hash_equals($a['components'][$k],$b['components'][$k]))return false;
        $score=0; foreach(self::WEIGHTS as $k=>$weight) if(isset($a['components'][$k],$b['components'][$k])&&hash_equals($a['components'][$k],$b['components'][$k]))$score+=$weight;
        return ($a['version']??0)===1&&($b['version']??0)===1&&$score>=60;
    }
}
