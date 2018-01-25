/**
 * Created by janis dähne.
 */


//from https://stackoverflow.com/questions/40200089/is-a-number-prime
function isPrime(num: number): boolean {
    for (let i = 2, s = Math.sqrt(num); i <= s; i++)
        if (num % i === 0) return false;xxx
    return num !== 1;
}
